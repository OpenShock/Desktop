﻿using System.IO.Pipes;
using System.Text.Json;
using OpenShock.Desktop.Utils;
using OpenShock.MinimalEvents;
using OpenShock.SDK.CSharp.Utils;

namespace OpenShock.Desktop.Services.Pipes;

public sealed class PipeServerService
{
    private readonly ILogger<PipeServerService> _logger;
    private uint _clientCount = 0;

    public PipeServerService(ILogger<PipeServerService> logger)
    {
        _logger = logger;
    }

    public IAsyncMinimalEventObservable<PipeMessageType> OnMessageReceived => _onMessageReceived;
    private readonly AsyncMinimalEvent<PipeMessageType>_onMessageReceived = new();
    
    public IAsyncMinimalEventObservable<string> OnTokenReceived => _onTokenReceived;
    private readonly AsyncMinimalEvent<string> _onTokenReceived = new();

    public void StartServer()
    {
        OsTask.Run(ServerLoop);
    }

    private async Task ServerLoop()
    {
        var id = _clientCount++;

        await using var pipeServerStream = new NamedPipeServerStream("OpenShock.Desktop", PipeDirection.In, 20,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);


        _logger.LogInformation("[{Id}] Starting new server loop", id);

        await pipeServerStream.WaitForConnectionAsync();
#pragma warning disable CS4014
        OsTask.Run(ServerLoop);
#pragma warning restore CS4014

        _logger.LogInformation("[{Id}] Pipe connected!", id);

        using var reader = new StreamReader(pipeServerStream);
        while (pipeServerStream.IsConnected && !reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line))
            {
                _logger.LogWarning("[{Id}] Received empty pipe message. Skipping...", id);
                continue;
            }

            try
            {
                var jsonObj = JsonSerializer.Deserialize<PipeMessage>(line);
                if (jsonObj is null)
                {
                    _logger.LogWarning("[{Id}] Failed to deserialize pipe message. Skipping...", id);
                    continue;
                }

                switch (jsonObj.Type)
                {
                    case PipeMessageType.Token:
                        var token = jsonObj.Data?.ToString();
                        if (string.IsNullOrEmpty(token))
                        {
                            _logger.LogWarning("[{Id}] Received empty token. Skipping...", id);
                            continue;
                        }
                        _logger.LogDebug("[{Id}] Received token: {Token}", id, token);
                        await OsTask.Run(() => _onTokenReceived.InvokeAsyncParallel(token));
                        break;
                    case PipeMessageType.Show:
                    default:
                        break;
                }

                await OsTask.Run(() => _onMessageReceived.InvokeAsyncParallel(jsonObj.Type)); // Task.Run to error handle in the event handler
                _logger.LogInformation("[{Id}], Received pipe message of type: {Type}", id, jsonObj.Type);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "[{Id}] Failed to deserialize pipe message. Skipping...", id);
            }
        }

        _logger.LogInformation("[{Id}] Pipe disconnected. Stopping server loop...", id);
    }
}