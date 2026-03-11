using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using System.Net.WebSockets;
using IOCompressionLevel = System.IO.Compression.CompressionLevel;


/// <summary>
/// 豆包 TTS v2（WebSocket 二进制协议 ws_binary）
/// - 接口：wss://openspeech.bytedance.com/api/v1/tts/ws_binary
/// - Header: Authorization: Bearer;{token}
/// - 发送：4B协议头 + 4B(payloadLen, BigEndian) + gzip(JSON)
/// - 收到：Audio-only server response 分片二进制，拼接为 mp3/wav/pcm 等
/// 
/// 注意：
/// 1) ClientWebSocket 一般仅在 PC/Console/Android/iOS 可用，WebGL 不支持
/// 2) wav 不支持流式（建议用 mp3/pcm）
/// </summary>
public class DoubaoTtsHttpClient : MonoBehaviour
{
    [Header("Doubao TTS v2 WebSocket (ws_binary)")]
    [Tooltip("火山控制台 AppID")]
    public string appId;

    [Tooltip("火山控制台 Token（Bearer;token）⚠️ 正式版不要放客户端")]
    public string token;

    [Tooltip("集群名（控制台给的 cluster）")]
    public string cluster = "volcano_tts";

    [Tooltip("音色ID（建议用 *_V2_streaming 之类的 v2 音色）")]
    public string voiceType = "BV700_V2_streaming";

    [Header("Audio")]
    public AudioSource audioSource;

    [Tooltip("建议 mp3：体积小；wav：更大但更兼容（但 wav 不支持流式）")]
    public string encoding = "mp3"; // mp3 / wav / pcm / ogg_opus

    [Tooltip("采样率，常见 24000/16000/8000（以控制台支持为准）")]
    public int sampleRate = 24000;

    [Range(0.2f, 3.0f)]
    public float speedRatio = 1.0f;

    [Range(0.1f, 3.0f)]
    public float volumeRatio = 1.0f;

    [Range(0.1f, 3.0f)]
    public float pitchRatio = 1.0f;

    const string WS_URL = "wss://openspeech.bytedance.com/api/v1/tts/ws_binary";

    [Serializable]
    class TtsReq
    {
        public App app;
        public User user;
        public Audio audio;
        public Request request;

        [Serializable] public class App { public string appid; public string token; public string cluster; }
        [Serializable] public class User { public string uid; }

        [Serializable]
        public class Audio
        {
            public string voice_type;
            public string encoding;
            public int rate;
            public float speed_ratio;
            public float volume_ratio;
            public float pitch_ratio;
            public string emotion; // 可选
            // 还有 language 等可扩展（按控制台/音色支持来）
        }

        [Serializable]
        public class Request
        {
            public string reqid;
            public string text;
            public string text_type;   // plain/ssml
            public string operation;   // submit（WebSocket要求）
            public int silence_duration; // 可选
        }
    }

    /// <summary>
    /// 对外入口：合成并播放（WebSocket流式，内部拼接）
    /// </summary>
    public void Speak(string text, string emotion = "neutral")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("[TTSv2] text is empty.");
            return;
        }
        if (!audioSource)
        {
            Debug.LogError("[TTSv2] AudioSource not assigned.");
            return;
        }

        StartCoroutine(CoSpeak(text, emotion));
    }

    System.Collections.IEnumerator CoSpeak(string text, string emotion)
    {
        bool done = false;
        byte[] audioBytes = null;
        Exception err = null;

        // 用 Task 跑 WebSocket（避免 Unity 主线程卡死）
        _ = Task.Run(async () =>
        {
            try
            {
                audioBytes = await RequestTtsBytesAsync(text, emotion);
            }
            catch (Exception e)
            {
                err = e;
            }
            finally
            {
                done = true;
            }
        });

        yield return new WaitUntil(() => done);

        if (err != null)
        {
            Debug.LogError("[TTSv2] " + err);
            yield break;
        }
        if (audioBytes == null || audioBytes.Length == 0)
        {
            Debug.LogError("[TTSv2] No audio bytes received.");
            yield break;
        }

        // 写临时文件（Unity 加载 mp3/wav 更稳）
        string ext = (encoding == "wav") ? "wav" : "mp3";
        string path = Path.Combine(Application.temporaryCachePath, $"tts_{Guid.NewGuid():N}.{ext}");
        try { File.WriteAllBytes(path, audioBytes); }
        catch (Exception e)
        {
            Debug.LogError($"[TTSv2] Write file failed: {e}\nPATH={path}");
            yield break;
        }

        // 加载并播放
        AudioType type = (encoding == "wav") ? AudioType.WAV : AudioType.MPEG;
        using var clipReq = UnityWebRequestMultimedia.GetAudioClip("file://" + path, type);
        clipReq.timeout = 20;
        yield return clipReq.SendWebRequest();

        if (clipReq.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[TTSv2] Load AudioClip failed: {clipReq.error}");
            yield break;
        }

        var clip = DownloadHandlerAudioClip.GetContent(clipReq);
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.Play();
    }

    async Task<byte[]> RequestTtsBytesAsync(string text, string emotion)
    {
        // 1) 组装 JSON（结构和你 v1 很像，但 operation=submit）
        var req = new TtsReq
        {
            app = new TtsReq.App
            {
                appid = appId,
                // 文档写“可传任意非空值”，但很多示例会填 "access_token"/占位符；鉴权以 Header 为准 :contentReference[oaicite:5]{index=5}
                token = "access_token",
                cluster = cluster
            },
            user = new TtsReq.User { uid = "unity" },
            audio = new TtsReq.Audio
            {
                voice_type = voiceType,
                encoding = encoding,
                rate = sampleRate,
                speed_ratio = speedRatio,
                volume_ratio = volumeRatio,
                pitch_ratio = pitchRatio,
                emotion = emotion
            },
            request = new TtsReq.Request
            {
                reqid = Guid.NewGuid().ToString(),
                text = text,
                text_type = "plain",
                operation = "submit",   // ✅ WebSocket 必须 submit :contentReference[oaicite:6]{index=6}
                silence_duration = 125
            }
        };

        string json = JsonUtility.ToJson(req);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        byte[] gzPayload = Gzip(jsonBytes);

        // 2) 打包二进制帧：4B header + 4B payloadLen(BE) + payload
        // header: [0x11, 0x10, 0x11, 0x00]
        // - version=1 + headerSize=1(4B) => 0x11
        // - msgType=1(full client request) + flags=0 => 0x10
        // - serialization=1(JSON) + compression=1(gzip) => 0x11 :contentReference[oaicite:7]{index=7}
        byte[] frame = BuildFullClientRequestFrame(gzPayload);

        // 3) 建立 WS
        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Authorization", $"Bearer;{token}"); // 分号 :contentReference[oaicite:8]{index=8}

        // 适度超时
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        await ws.ConnectAsync(new Uri(WS_URL), cts.Token);

        // 4) 发送请求
        await ws.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, true, cts.Token);

        // 5) 接收音频分片并拼接
        using var ms = new MemoryStream();
        var recvBuf = new byte[1024 * 64];

        while (ws.State == WebSocketState.Open)
        {
            // 一条 WS 消息可能分多次 Receive 才收完整
            using var oneMsg = new MemoryStream();
            WebSocketReceiveResult r;
            do
            {
                r = await ws.ReceiveAsync(new ArraySegment<byte>(recvBuf), cts.Token);
                if (r.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "client close", CancellationToken.None);
                    break;
                }
                oneMsg.Write(recvBuf, 0, r.Count);
            }
            while (!r.EndOfMessage);

            var resp = oneMsg.ToArray();
            if (resp.Length < 4) continue;

            // 解析 header
            int headerSizeWords = resp[0] & 0x0f;
            int headerBytes = headerSizeWords * 4;
            int msgType = (resp[1] >> 4) & 0x0f;
            int flags = resp[1] & 0x0f;
            int compression = resp[2] & 0x0f;

            if (resp.Length < headerBytes) continue;
            byte[] payload = new byte[resp.Length - headerBytes];
            Buffer.BlockCopy(resp, headerBytes, payload, 0, payload.Length);

            // msgType: 0xB Audio-only server response；0xF Error :contentReference[oaicite:9]{index=9}
            if (msgType == 0x0B)
            {
                // flags==0 通常是 started/ACK（无音频）
                if (flags == 0) continue;

                // 参考公开实现：payload 前 8B 是一些元信息（sequence 等），音频从 payload[8..] 开始 :contentReference[oaicite:10]{index=10}
                if (payload.Length > 8)
                {
                    ms.Write(payload, 8, payload.Length - 8);
                }

                // flags==3 表示最后一包（sequence<0 的结束包） :contentReference[oaicite:11]{index=11}
                if (flags == 3)
                {
                    try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); }
                    catch { /* ignore */ }
                    break;
                }
            }
            else if (msgType == 0x0F)
            {
                // error：payload[0..4) errorCode；payload[8..] error msg（可能 gzip） :contentReference[oaicite:12]{index=12}
                if (payload.Length >= 8)
                {
                    int errCode = ReadInt32BE(payload, 0);
                    byte[] errMsgBytes = new byte[payload.Length - 8];
                    Buffer.BlockCopy(payload, 8, errMsgBytes, 0, errMsgBytes.Length);

                    if (compression == 1)
                        errMsgBytes = Gunzip(errMsgBytes);

                    string errMsg = SafeUtf8(errMsgBytes);
                    throw new Exception($"[TTSv2] ServerError code={errCode} msg={errMsg}");
                }
                throw new Exception("[TTSv2] ServerError (unknown payload).");
            }
            // 其他 msgType：忽略
        }

        return ms.ToArray();
    }

    static byte[] BuildFullClientRequestFrame(byte[] gzPayload)
    {
        // default header: [0x11, 0x10, 0x11, 0x00] :contentReference[oaicite:13]{index=13}
        byte[] header = { 0x11, 0x10, 0x11, 0x00 };

        byte[] len = new byte[4];
        WriteUInt32BE(len, 0, (uint)gzPayload.Length);

        byte[] frame = new byte[header.Length + len.Length + gzPayload.Length];
        Buffer.BlockCopy(header, 0, frame, 0, header.Length);
        Buffer.BlockCopy(len, 0, frame, header.Length, len.Length);
        Buffer.BlockCopy(gzPayload, 0, frame, header.Length + len.Length, gzPayload.Length);
        return frame;
    }

    static byte[] Gzip(byte[] input)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, IOCompressionLevel.Optimal, leaveOpen: true))
        {
            gz.Write(input, 0, input.Length);
        }
        return ms.ToArray();
    }

    static byte[] Gunzip(byte[] input)
    {
        using var src = new MemoryStream(input);
        using var gz = new GZipStream(src, CompressionMode.Decompress);
        using var dst = new MemoryStream();
        gz.CopyTo(dst);
        return dst.ToArray();
    }

    static void WriteUInt32BE(byte[] buf, int offset, uint v)
    {
        buf[offset + 0] = (byte)((v >> 24) & 0xFF);
        buf[offset + 1] = (byte)((v >> 16) & 0xFF);
        buf[offset + 2] = (byte)((v >> 8) & 0xFF);
        buf[offset + 3] = (byte)(v & 0xFF);
    }

    static int ReadInt32BE(byte[] buf, int offset)
    {
        return (buf[offset + 0] << 24) |
               (buf[offset + 1] << 16) |
               (buf[offset + 2] << 8) |
               (buf[offset + 3]);
    }

    static string SafeUtf8(byte[] bytes)
    {
        try { return Encoding.UTF8.GetString(bytes); }
        catch { return "(non-utf8 error message)"; }
    }
}
