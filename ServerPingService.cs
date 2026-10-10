// Copyright (C) 2026 Raytolfas
// This file is part of Raytolfas Launcher.

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RaytolfasLauncher
{
    public record ServerPingResult(
        bool IsOnline,
        string Host,
        int Port,
        int OnlinePlayers,
        int MaxPlayers,
        string Version,
        string Motd,
        long PingMs
    );

    public static class ServerPingService
    {
        public static async Task<ServerPingResult> PingAsync(string serverAddress, int defaultPort = 25565, int timeoutMs = 2500, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(serverAddress))
                return new ServerPingResult(false, "", defaultPort, 0, 0, "", "Не указан адрес", -1);

            string host = serverAddress.Trim();
            int port = defaultPort;

            if (host.Contains(':'))
            {
                var parts = host.Split(':', 2);
                host = parts[0].Trim();
                if (int.TryParse(parts[1].Trim(), out int parsedPort) && parsedPort > 0)
                {
                    port = parsedPort;
                }
            }

            var sw = Stopwatch.StartNew();

            try
            {
                using var client = new TcpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeoutMs);

                await client.ConnectAsync(host, port, cts.Token);
                using var stream = client.GetStream();
                stream.ReadTimeout = timeoutMs;
                stream.WriteTimeout = timeoutMs;

                using (var ms = new MemoryStream())
                {
                    WriteVarInt(ms, 0x00);
                    WriteVarInt(ms, 763);
                    WriteString(ms, host);
                    WriteUShort(ms, (ushort)port);
                    WriteVarInt(ms, 1);

                    byte[] handshakeBytes = ms.ToArray();
                    using var packetMs = new MemoryStream();
                    WriteVarInt(packetMs, handshakeBytes.Length);
                    packetMs.Write(handshakeBytes, 0, handshakeBytes.Length);
                    byte[] payload = packetMs.ToArray();
                    await stream.WriteAsync(payload, 0, payload.Length, cts.Token);
                }

                {
                    byte[] req = new byte[] { 0x01, 0x00 };
                    await stream.WriteAsync(req, 0, req.Length, cts.Token);
                }

                int packetLength = ReadVarInt(stream);
                int packetId = ReadVarInt(stream);
                if (packetId != 0x00)
                {
                    return new ServerPingResult(false, host, port, 0, 0, "", "Неверный ответ сервера", -1);
                }

                int jsonLength = ReadVarInt(stream);
                byte[] jsonBytes = new byte[jsonLength];
                int totalRead = 0;
                while (totalRead < jsonLength)
                {
                    int r = await stream.ReadAsync(jsonBytes, totalRead, jsonLength - totalRead, cts.Token);
                    if (r <= 0) break;
                    totalRead += r;
                }

                sw.Stop();
                long pingMs = sw.ElapsedMilliseconds;

                string json = Encoding.UTF8.GetString(jsonBytes, 0, totalRead);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                int online = 0;
                int max = 0;
                string version = "";
                string motd = "";

                if (root.TryGetProperty("players", out var playersProp))
                {
                    if (playersProp.TryGetProperty("online", out var onlineProp))
                        online = onlineProp.GetInt32();
                    if (playersProp.TryGetProperty("max", out var maxProp))
                        max = maxProp.GetInt32();
                }

                if (root.TryGetProperty("version", out var versionProp) && versionProp.TryGetProperty("name", out var versionNameProp))
                {
                    version = versionNameProp.GetString() ?? "";
                }

                if (root.TryGetProperty("description", out var descProp))
                {
                    if (descProp.ValueKind == JsonValueKind.String)
                        motd = descProp.GetString() ?? "";
                    else if (descProp.TryGetProperty("text", out var textProp))
                        motd = textProp.GetString() ?? "";
                }

                return new ServerPingResult(true, host, port, online, max, version, motd, pingMs);
            }
            catch (Exception)
            {
                sw.Stop();
                return new ServerPingResult(false, host, port, 0, 0, "", "Офлайн или недоступен", -1);
            }
        }

        private static void WriteVarInt(Stream stream, int value)
        {
            while ((value & -128) != 0)
            {
                stream.WriteByte((byte)(value & 127 | 128));
                value = (int)((uint)value >> 7);
            }
            stream.WriteByte((byte)value);
        }

        private static int ReadVarInt(Stream stream)
        {
            int numRead = 0;
            int result = 0;
            byte read;
            do
            {
                int val = stream.ReadByte();
                if (val == -1) throw new EndOfStreamException();
                read = (byte)val;
                int value = read & 127;
                result |= value << 7 * numRead;
                numRead++;
                if (numRead > 5) throw new InvalidOperationException("VarInt is too big");
            } while ((read & 128) != 0);

            return result;
        }

        private static void WriteString(Stream stream, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            WriteVarInt(stream, bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteUShort(Stream stream, ushort value)
        {
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)(value & 0xFF));
        }
    }
}
