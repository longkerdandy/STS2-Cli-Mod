using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using STS2.Cli.Cmd.Models.Message;
using STS2.Cli.Cmd.Utils;

namespace STS2.Cli.Cmd.Services;

/// <summary>
///     Named Pipe client for communicating with the STS2 mod.
///     This class manages the connection to the mod's named pipe server and provides
///     methods for sending commands and receiving responses. It implements IDisposable
///     to ensure proper cleanup of pipe and stream resources.
/// </summary>
public class PipeClient : IDisposable
{
    private const string PipeName = "sts2-cli-mod";
    private const string ServerName = "."; // Local machine
    private const int DefaultTimeoutMs = 5000;

    private NamedPipeClientStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    /// <summary>
    ///     Releases all resources used by the <see cref="PipeClient" />.
    /// </summary>
    public void Dispose()
    {
        // Reader/Writer use leaveOpen: true, so they won't close the pipe
        // Just clean up references and dispose of the pipe
        _reader = null;
        _writer = null;
        _pipe?.Dispose();
        _pipe = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Connects to the mod's named pipe server.
    /// </summary>
    /// <param name="timeoutMs">Connection timeout in milliseconds. Default is 5000ms.</param>
    /// <returns>
    ///     <c>true</c> if connected successfully; <c>false</c> if connection failed,
    ///     timed out, or the pipe server is not available.
    /// </returns>
    public async Task<bool> ConnectAsync(int timeoutMs = DefaultTimeoutMs)
    {
        try
        {
            _pipe = new NamedPipeClientStream(
                ServerName,
                PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            // Use CancellationToken for reliable timeout
            using var cts = new CancellationTokenSource(timeoutMs);
            await _pipe.ConnectAsync(cts.Token);

            if (_pipe.IsConnected)
            {
                // leaveOpen: true - we manage pipe lifecycle separately
                _reader = new StreamReader(_pipe, Encoding.UTF8, leaveOpen: true);
                _writer = new StreamWriter(_pipe, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                return true;
            }
        }
        catch
        {
            // OperationCanceledException (timeout), TimeoutException, IOException, etc.
            // All mean the same thing: connection failed — clean up and return false
            if (_pipe != null) await _pipe.DisposeAsync();
            _pipe = null;
        }

        return false;
    }

    /// <summary>
    ///     Sends a command to the mod and returns the response.
    /// </summary>
    /// <param name="cmd">The command to send (e.g., "ping", "state", "play_card").</param>
    /// <param name="args">Optional array of integer arguments for the command.</param>
    /// <param name="target">Optional target combat ID for targeted commands.</param>
    /// <returns>
    ///     A <see cref="Response" /> object containing the result from the mod,
    ///     or <c>null</c> if the pipe is not connected or communication failed.
    /// </returns>
    public async Task<Response?> SendCommandAsync(string cmd, int[]? args = null, int? target = null)
    {
        if (_pipe is not { IsConnected: true }) return null;

        try
        {
            var request = new Request
            {
                Cmd = cmd,
                Args = args,
                Target = target
            };

            var requestJson = JsonSerializer.Serialize(request, JsonOptions.Default);
            await _writer!.WriteLineAsync(requestJson);

            var responseJson = await _reader!.ReadLineAsync();
            return responseJson == null ? null : JsonSerializer.Deserialize<Response>(responseJson, JsonOptions.Default);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    ///     Sends a command with ID-based parameters to the mod and returns the response.
    /// </summary>
    /// <param name="cmd">The command to send (e.g., "play_card", "use_potion").</param>
    /// <param name="itemId">Card ID or Potion ID (required).</param>
    /// <param name="nth">N-th occurrence when multiple copies exist (0-based).</param>
    /// <param name="target">Optional target combat ID for targeted commands.</param>
    /// <param name="isCard">True if this is a card command (play_card), false for potion.</param>
    /// <returns>
    ///     A <see cref="Response" /> object containing the result from the mod,
    ///     or <c>null</c> if the pipe is not connected or communication failed.
    /// </returns>
    public async Task<Response?> SendCommandAsync(
        string cmd,
        string itemId,
        int nth,
        int? target = null,
        bool isCard = true)
    {
        if (_pipe is not { IsConnected: true }) return null;

        try
        {
            var request = new Request
            {
                Cmd = cmd,
                CardId = isCard ? itemId : null,
                PotionId = isCard ? null : itemId,
                Nth = nth,
                Target = target
            };

            var requestJson = JsonSerializer.Serialize(request, JsonOptions.Default);
            await _writer!.WriteLineAsync(requestJson);

            var responseJson = await _reader!.ReadLineAsync();
            return responseJson == null ? null : JsonSerializer.Deserialize<Response>(responseJson, JsonOptions.Default);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
