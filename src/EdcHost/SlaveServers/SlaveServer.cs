using Serilog;

namespace EdcHost.SlaveServers;

public class SlaveServer : ISlaveServer
{

    public event EventHandler<PlayerTryAttackEventArgs> PlayerTryAttackEvent;
    public event EventHandler<PlayerTryPlaceBlockEventArgs> PlayerTryPlaceBlockEvent;
    public event EventHandler<PlayerTryTradeEventArgs> PlayerTryTradeEvent;

    public List<string> AvailablePortNames => _serialPortHub.PortNames;

    bool _isRunning = false;
    readonly ILogger _logger = Log.Logger.ForContext("Component", "SlaveServers");
    readonly ISerialPortHub _serialPortHub;
    readonly List<ISerialPortWrapper> _serialPorts = new();

    public SlaveServer(ISerialPortHub serialPortHub)
    {
        _serialPortHub = serialPortHub;
    }

    public void OpenPort(string portName)
    {
        if (_serialPorts.Any(x => x.PortName.Equals(portName)))
        {
            throw new ArgumentException($"port name already exists: {portName}");
        }

        ISerialPortWrapper serialPort = _serialPortHub.Get(portName);
        serialPort.AfterReceive += (sender, args) =>
        {
            PerformAction(args.PortName, new PacketFromSlave(args.Bytes));
        };

        serialPort.Open();
        _serialPorts.Add(serialPort);
    }

    public void ClosePort(string portName)
    {
        ISerialPortWrapper? serialPort = _serialPorts.Find(x => x.PortName.Equals(portName)) ??
            throw new ArgumentException($"port name does not exist: {portName}");

        serialPort.Close();
        _serialPorts.Remove(serialPort);
    }

    public void Publish(string portName, int gameStage, int elapsedTime, List<int> heightOfChunks,
        bool hasBed, bool hasBedOpponent, double positionX, double positionY, double positionOpponentX,
        double positionOpponentY, int agility, int health, int maxHealth, int strength,
        int emeraldCount, int woolCount)
    {
        ISerialPortWrapper? serialPort = _serialPorts.Find(x => x.PortName.Equals(portName)) ??
            throw new ArgumentException($"port name does not exist: {portName}");

        IPacket packet = new PacketFromHost(gameStage, elapsedTime, heightOfChunks,
            hasBed, hasBedOpponent, (float)positionX, (float)positionY, (float)positionOpponentX, (float)positionOpponentY, agility, health, maxHealth, strength,
            emeraldCount, woolCount);
        byte[] bytes = packet.ToBytes();
        serialPort.Send(bytes);
    }

    public void Start()
    {
        if (_isRunning)
        {
            throw new InvalidOperationException("already running");
        }

        _logger.Information("Starting...");

        _isRunning = true;

        _logger.Information("Started.");
    }

    public void Stop()
    {
        if (!_isRunning)
        {
            throw new InvalidOperationException("not running");
        }

        _logger.Information("Stopping...");

        foreach (ISerialPortWrapper serialPort in _serialPorts)
        {
            serialPort.Dispose();
        }
        _serialPorts.Clear();

        _isRunning = false;

        _logger.Information("Stopped.");
    }

    void PerformAction(string portName, IPacketFromSlave packet)
    {
        switch (packet.ActionType)
        {
            case (int)ActionKind.Attack:
                PlayerTryAttackEvent?.Invoke(this, new PlayerTryAttackEventArgs(portName, packet.Param));
                break;

            case (int)ActionKind.Use:
                PlayerTryPlaceBlockEvent?.Invoke(this, new PlayerTryPlaceBlockEventArgs(portName, packet.Param));
                break;

            case (int)ActionKind.Trade:
                PlayerTryTradeEvent?.Invoke(this, new PlayerTryTradeEventArgs(portName, packet.Param));
                break;

            default:
                break;
        }
    }
}
