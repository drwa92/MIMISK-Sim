using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using UnityEngine;

using MimiskBridgeClient = MIMISK.Grpc.MIMISKBridge.MIMISKBridgeClient;
using MimiskPingReply = MIMISK.Grpc.PingReply;
using MimiskPingRequest = MIMISK.Grpc.PingRequest;

[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public class MIMISKGrpcConnection : MonoBehaviour
{
    [Header("Server")]
    public string serverIP = "localhost";
    public int serverPort = 30052;
    public string clientName = "mimisk_unity_v2";

    [Header("Connection")]
    public bool connectOnStart = true;
    public bool reconnectIfFailed = true;
    public float reconnectPeriodS = 2.0f;

    [Header("Runtime")]
    public bool isConnected;
    public string lastStatus = "not_started";
    public float lastPingTimeS;

    public Channel Channel
    {
        get { return channel; }
    }

    public MimiskBridgeClient Client
    {
        get { return client; }
    }

    private Channel channel;
    private MimiskBridgeClient client;
    private float reconnectTimerS;
    private bool connectInProgress;

    private void Start()
    {
        if (connectOnStart)
        {
            Connect();
        }
    }

    private void Update()
    {
        if (isConnected || !reconnectIfFailed || connectInProgress)
        {
            return;
        }

        reconnectTimerS += Time.unscaledDeltaTime;

        if (reconnectTimerS >= reconnectPeriodS)
        {
            reconnectTimerS = 0.0f;
            Connect();
        }
    }

    [ContextMenu("Connect")]
    public async void Connect()
    {
        if (connectInProgress)
        {
            return;
        }

        connectInProgress = true;

        try
        {
            await ShutdownChannel();

            string address =
                serverIP + ":" + serverPort.ToString();

            List<ChannelOption> options =
                new List<ChannelOption>
                {
                    new ChannelOption(
                        "grpc.max_send_message_length",
                        100 * 1024 * 1024
                    ),
                    new ChannelOption(
                        "grpc.max_receive_message_length",
                        100 * 1024 * 1024
                    )
                };

            channel =
                new Channel(
                    address,
                    ChannelCredentials.Insecure,
                    options
                );

            client =
                new MimiskBridgeClient(channel);

            MimiskPingReply reply =
                await client.PingAsync(
                    new MimiskPingRequest
                    {
                        ClientName = clientName
                    }
                );

            isConnected =
                reply.Ok;

            lastStatus =
                reply.Message;

            lastPingTimeS =
                Time.unscaledTime;

            Debug.Log(
                "[MIMISK gRPC] Connected to " +
                address +
                " | " +
                lastStatus
            );
        }
        catch (Exception ex)
        {
            isConnected =
                false;

            lastStatus =
                ex.GetType().Name + ": " + ex.Message;

            Debug.LogWarning(
                "[MIMISK gRPC] Connection failed: " +
                lastStatus
            );
        }
        finally
        {
            connectInProgress =
                false;
        }
    }

    [ContextMenu("Ping")]
    public async void Ping()
    {
        if (client == null)
        {
            Connect();
            return;
        }

        try
        {
            MimiskPingReply reply =
                await client.PingAsync(
                    new MimiskPingRequest
                    {
                        ClientName = clientName
                    }
                );

            isConnected =
                reply.Ok;

            lastStatus =
                reply.Message;

            lastPingTimeS =
                Time.unscaledTime;

            Debug.Log(
                "[MIMISK gRPC] Ping: " +
                lastStatus
            );
        }
        catch (Exception ex)
        {
            isConnected =
                false;

            lastStatus =
                ex.GetType().Name + ": " + ex.Message;

            Debug.LogWarning(
                "[MIMISK gRPC] Ping failed: " +
                lastStatus
            );
        }
    }

    private async Task ShutdownChannel()
    {
        if (channel != null)
        {
            try
            {
                await channel.ShutdownAsync();
            }
            catch
            {
            }
        }

        channel =
            null;

        client =
            null;

        isConnected =
            false;
    }

    private async void OnDestroy()
    {
        await ShutdownChannel();
    }

    private async void OnApplicationQuit()
    {
        await ShutdownChannel();
    }
}
