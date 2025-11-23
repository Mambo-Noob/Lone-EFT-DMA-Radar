/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
 * 
MIT License

Copyright (c) 2025 Lone DMA

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 *
*/

using Collections.Pooled;
using LoneEftDmaRadar.Misc;
using LoneEftDmaRadar.Tarkov.GameWorld;
using LoneEftDmaRadar.Tarkov.GameWorld.Camera;
using LoneEftDmaRadar.Tarkov.GameWorld.Exits;
using LoneEftDmaRadar.Tarkov.GameWorld.Explosives;
using LoneEftDmaRadar.Tarkov.GameWorld.Loot.Helpers;
using LoneEftDmaRadar.Tarkov.GameWorld.Player;
using LoneEftDmaRadar.Tarkov.Unity.Structures;
using LoneEftDmaRadar.UI.Misc;
using VmmSharpEx;
using VmmSharpEx.Extensions;
using VmmSharpEx.Options;
using VmmSharpEx.Refresh;
using VmmSharpEx.Scatter;

namespace LoneEftDmaRadar.DMA
{
    /// <summary>
    /// DMA Memory Module.
    /// </summary>
    public sealed class MemDMA : IDisposable
    {
        #region Init
        private MakcuAimbot _makcuAimbot;
        public static MakcuAimbot MakcuAimbot { get; private set; }
        private const string GAME_PROCESS_NAME = "EscapeFromTarkov.exe";
        internal const uint MAX_READ_SIZE = 0x1000u * 1500u;
        private static readonly string _mmap = Path.Combine(App.ConfigPath.FullName, "mmap.txt");
        private readonly Vmm _vmm;
        private readonly InputManager _input;
        private uint _pid;
        private bool _restartRadar;

        public string MapID => Game?.MapID;
        public ulong UnityBase { get; private set; }
        public uint UnitySize { get; set; }
        public ulong GOM { get; private set; }
        public bool Starting { get; private set; }
        public bool Ready { get; private set; }
        public bool InRaid => Game?.InRaid ?? false;
        public static CameraManager CameraManager { get; internal set; }
        private static CameraManager _cameraManager;
        #region Restart Radar

        private readonly Lock _restartSync = new();
        private CancellationTokenSource _cts = new();
        /// <summary>
        /// Signal the Radar to restart the raid/game loop.
        /// </summary>
        public void RestartRadar()
        {
            lock (_restartSync)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = new();
                Restart = _cts.Token;
            }
        }
        /// <summary>
        /// Cancellation Token that is triggered when the Radar should restart the raid/game loop.
        /// </summary>
        public CancellationToken Restart { get; private set; }

        #endregion

        public IReadOnlyCollection<AbstractPlayer> Players => Game?.Players;
        public IReadOnlyCollection<IExplosiveItem> Explosives => Game?.Explosives;
        public IReadOnlyCollection<IExitPoint> Exits => Game?.Exits;
        public LocalPlayer LocalPlayer => Game?.LocalPlayer;
        public LootManager Loot => Game?.Loot;
        public LocalGameWorld Game { get; private set; }

        internal MemDMA()
        {
            Restart = _cts.Token;
            FpgaAlgo fpgaAlgo = App.Config.DMA.FpgaAlgo;
            bool useMemMap = App.Config.DMA.MemMapEnabled;
            Debug.WriteLine("Initializing DMA...");
            /// Check MemProcFS Versions...
            string vmmVersion = FileVersionInfo.GetVersionInfo("vmm.dll").FileVersion;
            string lcVersion = FileVersionInfo.GetVersionInfo("leechcore.dll").FileVersion;
            string versions = $"Vmm Version: {vmmVersion}\n" +
                $"Leechcore Version: {lcVersion}";
            string[] initArgs = new[] {
                "-norefresh",
                "-device",
                fpgaAlgo is FpgaAlgo.Auto ?
                    "fpga" : $"fpga://algo={(int)fpgaAlgo}",
                "-waitinitialize"};
            try
            {
                /// Begin Init...
                if (useMemMap)
                {
                    if (!File.Exists(_mmap))
                    {
                        Debug.WriteLine("[DMA] No MemMap, attempting to generate...");
                        _vmm = new Vmm(args: initArgs)
                        {
                            EnableMemoryWriting = true
                        };
                        _ = _vmm.GetMemoryMap(
                            applyMap: true,
                            outputFile: _mmap);
                    }
                    else
                    {
                        var mapArgs = new[] { "-memmap", _mmap };
                        initArgs = initArgs.Concat(mapArgs).ToArray();
                    }
                }
                _vmm ??= new Vmm(args: initArgs)
                {
                    EnableMemoryWriting = true
                };
                _vmm.RegisterAutoRefresh(RefreshOption.MemoryPartial, TimeSpan.FromMilliseconds(300));
                _vmm.RegisterAutoRefresh(RefreshOption.TlbPartial, TimeSpan.FromSeconds(2));
                try
                {
                    _input = new(_vmm);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        messageBoxText: $"WARNING: Failed to initialize InputManager (win32). Please note, this only works on Windows 11 (Game PC). Startup will continue without hotkeys.\n\n{ex}",
                        caption: App.Name,
                        button: MessageBoxButton.OK,
                        icon: MessageBoxImage.Warning,
                        defaultResult: MessageBoxResult.OK,
                        options: MessageBoxOptions.DefaultDesktopOnly);
                }
                ProcessStopped += MemDMA_ProcessStopped;
                RaidStopped += MemDMA_RaidStopped;
                // Start Memory Thread after successful startup
                new Thread(MemoryPrimaryWorker)
                {
                    IsBackground = true
                }.Start();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                "DMA Initialization Failed!\n" +
                $"Reason: {ex.Message}\n" +
                $"{versions}\n\n" +
                "===TROUBLESHOOTING===\n" +
                "1. Reboot both your Game PC / Radar PC (This USUALLY fixes it).\n" +
                "2. Reseat all cables/connections and make sure they are secure.\n" +
                "3. Changed Hardware/Operating System on Game PC? Reset your DMA Config ('Options' menu in Client) and try again.\n" +
                "4. Make sure all Setup Steps are completed (See DMA Setup Guide/FAQ for additional troubleshooting).\n\n" +
                "PLEASE REVIEW THE ABOVE BEFORE CONTACTING SUPPORT!");
            }
        }

        /// <summary>
        /// Main worker thread to perform DMA Reads on.
        /// </summary>
        private void MemoryPrimaryWorker()
        {
            Debug.WriteLine("Memory thread starting...");
            while (MainWindow.Instance is null)
                Thread.Sleep(1);

            while (true)
            {
                try
                {
                    while (true) // Main Loop
                    {
                        RunStartupLoop();
                        OnProcessStarted();

                        // Create Makcu aimbot for this MemDMA instance
                        _makcuAimbot = new MakcuAimbot(this);
                        MakcuAimbot = _makcuAimbot;   // expose globally
                        Debug.WriteLine("[MemDMA] MakcuAimbot created");

                        RunGameLoop();
                        OnProcessStopped();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"FATAL ERROR on Memory Thread: {ex}");
                    OnProcessStopped();
                    Thread.Sleep(1000);
                }
            }
        }


        #endregion

        #region Startup / Main Loop

        /// <summary>
        /// Starts up the Game Process and all mandatory modules.
        /// Returns to caller when the Game is ready.
        /// </summary>
// Add field at top of MemDMA class
private Thread _cameraThread;
private volatile bool _cameraThreadRunning;

// In RunStartupLoop, after CameraManager initialization:
private void RunStartupLoop()
{
    Debug.WriteLine("New Process Startup");
    while (true)
    {
        try
        {
            _vmm.ForceFullRefresh();
            ResourceJanitor.Run();
            LoadProcess();
            LoadModules();
            this.Starting = true;
            OnProcessStarting();
            try
            {
                _cameraManager = new CameraManager();
                MemDMA.CameraManager = _cameraManager;
                CameraManager.UpdateViewportRes();
                Debug.WriteLine("CameraManager initialized successfully!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR initializing CameraManager: {ex}");
            }
            this.Ready = true;
            Debug.WriteLine("Process Startup [OK]");
            break;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Process Startup [FAIL]: {ex}");
            OnProcessStopped();
            Thread.Sleep(1000);
        }
    }
}

// In RunGameLoop, start camera thread when raid starts:
private void RunGameLoop()
{
    while (true)
    {
        try
        {
            var ct = Restart;
            using (var game = Game = LocalGameWorld.CreateGameInstance(ct))
            {
                OnRaidStarted();
                
                game.Start();
                while (game.InRaid)
                {
                    ct.ThrowIfCancellationRequested();
                    game.Refresh();
                    Thread.Sleep(133); // Game loop at 60Hz
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            Debug.WriteLine(ex.Message);
            continue;
        }
        catch (ProcessNotRunningException ex)
        {
            Debug.WriteLine(ex.Message);
            break;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unhandled Exception in Game Loop: {ex}");
            break;
        }
        finally
        {
            // ✅ REMOVED: StopCameraThread() - no longer needed!
            
            OnRaidStopped();
            Thread.Sleep(100);
        }
    }
}

[DllImport("kernel32.dll")]
private static extern IntPtr GetCurrentThread();

[DllImport("kernel32.dll")]
private static extern bool SetThreadPriority(IntPtr hThread, int nPriority);

private const int THREAD_PRIORITY_TIME_CRITICAL = 15;

private void StartCameraThread()
{
    _cameraThreadRunning = true;
    _cameraThread = new Thread(() =>
    {
        Debug.WriteLine("[Camera] High-frequency thread started (1000Hz)");
        
        // ✅ Set to time-critical priority for immediate scheduling
        try
        {
            SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_TIME_CRITICAL);
            Debug.WriteLine("[Camera] Thread priority set to TIME_CRITICAL");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Camera] Failed to set thread priority: {ex}");
        }
        
        while (_cameraThreadRunning && Ready && InRaid)
        {
            try
            {
                var lp = LocalPlayer;
                if (lp != null && _cameraManager != null)
                {
                    // ✅ NOCACHE ensures fresh data every time
                    using var scatter = CreateScatter(VmmFlags.NOCACHE);
                    _cameraManager.OnRealtimeLoop(scatter, lp);
                    // Execute() is called inside OnRealtimeLoop now (synchronous)
                }
                
                // ✅ 1ms = 1000Hz update rate
                Thread.Sleep(1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Camera] Update error: {ex.Message}");
                Thread.Sleep(50);
            }
        }
        
        Debug.WriteLine("[Camera] High-frequency thread stopped");
    })
    {
        IsBackground = true,
        Priority = ThreadPriority.Highest, // .NET priority
        Name = "CameraHighFreqThread"
    };
    
    _cameraThread.Start();
}

private void StopCameraThread()
{
    _cameraThreadRunning = false;
    _cameraThread = null;
}

        /// <summary>
        /// Raised when the game is stopped.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MemDMA_ProcessStopped(object sender, EventArgs e)
        {
            _restartRadar = default;
            this.Starting = default;
            this.Ready = default;
            UnityBase = default;
            GOM = default;
            _pid = default;
            StopCameraThread();

            _makcuAimbot?.Dispose();
            _makcuAimbot = null;
            MakcuAimbot = null;    // clear static reference
        }


        private void MemDMA_RaidStopped(object sender, EventArgs e)
        {
            Game = null;
        }

        /// <summary>
        /// Obtain the PID for the Game Process.
        /// </summary>
        private void LoadProcess()
        {

            if (!_vmm.PidGetFromName(GAME_PROCESS_NAME, out uint pid))
                throw new InvalidOperationException($"Unable to find '{GAME_PROCESS_NAME}'");
            _pid = pid;
        }

        /// <summary>
        /// Gets the Game Process Base Module Addresses.
        /// </summary>
        private void LoadModules()
        {
            var unityBase = _vmm.ProcessGetModuleBase(_pid, "UnityPlayer.dll");
            unityBase.ThrowIfInvalidVirtualAddress(nameof(unityBase));
            GOM = GameObjectManager.GetAddr(unityBase);
            UnityBase = unityBase;
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when the game process is starting up (after getting PID/Module Base).
        /// Outside Subscribers should handle exceptions!
        /// </summary>
        public static event EventHandler<EventArgs> ProcessStarting;
        /// <summary>
        /// Raised when the game process is successfully started.
        /// Outside Subscribers should handle exceptions!
        /// </summary>
        public static event EventHandler<EventArgs> ProcessStarted;
        /// <summary>
        /// Raised when the game process is no longer running.
        /// Outside Subscribers should handle exceptions!
        /// </summary>
        public static event EventHandler<EventArgs> ProcessStopped;
        /// <summary>
        /// Raised when a raid starts.
        /// Outside Subscribers should handle exceptions!
        /// </summary>
        public static event EventHandler<EventArgs> RaidStarted;
        /// <summary>
        /// Raised when a raid ends.
        /// Outside Subscribers should handle exceptions!
        /// </summary>
        public static event EventHandler<EventArgs> RaidStopped;

        /// <summary>
        /// Raises the ProcessStarting Event.
        /// </summary>
        private static void OnProcessStarting()
        {
            ProcessStarting?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the ProcessStarted Event.
        /// </summary>
        private static void OnProcessStarted()
        {
            ProcessStarted?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the ProcessStopped Event.
        /// </summary>
        private static void OnProcessStopped()
        {
            ProcessStopped?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the RaidStarted Event.
        /// </summary>
        private static void OnRaidStarted()
        {
            RaidStarted?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the RaidStopped Event.
        /// </summary>
        private static void OnRaidStopped()
        {
            RaidStopped?.Invoke(null, EventArgs.Empty);
        }

        #endregion

        #region Read Methods

        /// <summary>
        /// Prefetch pages into the cache.
        /// </summary>
        /// <param name="va"></param>
        public void ReadCache(params ulong[] va)
        {
            _vmm.MemPrefetchPages(_pid, va);
        }

        /// <summary>
        /// Read memory into a Buffer of type <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T">Value Type <typeparamref name="T"/></typeparam>
        /// <param name="addr">Virtual Address to read from.</param>
        /// <param name="span">Buffer to receive memory read in.</param>
        /// <param name="useCache">Use caching for this read.</param>
        public void ReadSpan<T>(ulong addr, Span<T> span, bool useCache = true)
            where T : unmanaged
        {
            uint cb = (uint)checked(Unsafe.SizeOf<T>() * span.Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(cb, MAX_READ_SIZE, nameof(cb));
            var flags = useCache ? VmmFlags.NONE : VmmFlags.NOCACHE;

            if (!_vmm.MemReadSpan(_pid, addr, span, flags))
                throw new VmmException("Memory Read Failed!");
        }

        /// <summary>
        /// Read memory into a Buffer of type <typeparamref name="T"/> and ensure the read is correct.
        /// </summary>
        /// <typeparam name="T">Value Type <typeparamref name="T"/></typeparam>
        /// <param name="addr">Virtual Address to read from.</param>
        /// <param name="span">Buffer to receive memory read in.</param>
        public void ReadSpanEnsure<T>(ulong addr, Span<T> span)
            where T : unmanaged
        {
            uint cb = (uint)checked(Unsafe.SizeOf<T>() * span.Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(cb, MAX_READ_SIZE, nameof(cb));
            var buffer2 = new T[span.Length].AsSpan();
            var buffer3 = new T[span.Length].AsSpan();
            if (!_vmm.MemReadSpan(_pid, addr, buffer3, VmmFlags.NOCACHE))
                throw new VmmException("Memory Read Failed!");
            Thread.SpinWait(5);
            if (!_vmm.MemReadSpan(_pid, addr, buffer2, VmmFlags.NOCACHE))
                throw new VmmException("Memory Read Failed!");
            Thread.SpinWait(5);
            if (!_vmm.MemReadSpan(_pid, addr, span, VmmFlags.NOCACHE))
                throw new VmmException("Memory Read Failed!");
            if (!span.SequenceEqual(buffer2) || !span.SequenceEqual(buffer3) || !buffer2.SequenceEqual(buffer3))
            {
                throw new VmmException("Memory Read Failed!");
            }
        }

        /// <summary>
        /// Read an array of type <typeparamref name="T"/> from memory.
        /// The first element begins reading at 0x0 and the array is assumed to be contiguous.
        /// IMPORTANT: You must call <see cref="IDisposable.Dispose"/> on the returned SharedArray when done."/>
        /// </summary>
        /// <typeparam name="T">Value type to read.</typeparam>
        /// <param name="addr">Address to read from.</param>
        /// <param name="count">Number of array elements to read.</param>
        /// <param name="useCache">Use caching for this read.</param>
        /// <returns><see cref="PooledMemory{T}"/> value. Be sure to call <see cref="IDisposable.Dispose"/>!</returns>
        public PooledMemory<T> ReadArray<T>(ulong addr, int count, bool useCache = true)
            where T : unmanaged
        {
            var flags = useCache ? VmmFlags.NONE : VmmFlags.NOCACHE;
            var arr = _vmm.MemReadArray<T>(_pid, addr, count, flags) ??
                throw new VmmException("Memory Read Failed!");
            return arr;
        }
        public byte[] ReadBytes(ulong address, uint size, bool useCached)
        {
            try
            {
                var buffer = new byte[size];
                // Use instance ReadSpan to fill the buffer
                ReadSpan(address, buffer, useCached);
                return buffer;
            }
            catch
            {
                return null;
            }
        }
        private static ulong SafeReadPtr(ulong addr, string fieldName)
        {
            if (addr == 0)
                throw new InvalidOperationException($"{fieldName}: source address is 0x0");

            var value = Memory.ReadPtr(addr, false);
            if (value == 0)
                throw new InvalidOperationException($"{fieldName}: read value is 0x0 (bad offset / not initialized)");

            return value;
        }

        private static T SafeReadValue<T>(ulong addr, string fieldName) where T : unmanaged
        {
            if (addr == 0)
                throw new InvalidOperationException($"{fieldName}: address is 0x0");

            return Memory.ReadValue<T>(addr, false);
        }

        /// <summary>
        /// Wrapper for ReadPtrChain that asserts non-zero and logs context.
        /// </summary>
        private static ulong SafeReadPtrChain(string fieldName, ulong root, params uint[] chain)
        {
            var value = Memory.ReadPtrChain(root, false, chain);
            if (value == 0)
                throw new InvalidOperationException($"{fieldName}: ReadPtrChain returned 0 (bad chain / offsets)");

            return value;
        }
        /// <summary>
        /// Read a chain of pointers and get the final result.
        /// </summary>
        /// <param name="addr">Base virtual address to read from.</param>
        /// <param name="useCache">Use caching for this read (recommended).</param>
        /// <param name="offsets">Offsets to read in succession.</param>
        /// <returns>Pointer address after final offset.</returns>
        public ulong ReadPtrChain(ulong addr, bool useCache, params Span<uint> offsets)
        {
            ulong pointer = addr;
            foreach (var offset in offsets)
            {
                pointer = ReadPtr(checked(pointer + offset), useCache);
            }
            return pointer;
        }

        /// <summary>
        /// Resolves a pointer and returns the memory address it points to.
        /// </summary>
        public ulong ReadPtr(ulong addr, bool useCache = true)
        {
            var pointer = ReadValue<VmmPointer>(addr, useCache);
            pointer.ThrowIfInvalid();
            return pointer;
        }

        /// <summary>
        /// Read value type/struct from specified address.
        /// </summary>
        /// <typeparam name="T">Specified Value Type.</typeparam>
        /// <param name="addr">Address to read from.</param>
        public T ReadValue<T>(ulong addr, bool useCache = true)
            where T : unmanaged, allows ref struct
        {
            var flags = useCache ? VmmFlags.NONE : VmmFlags.NOCACHE;
            if (!_vmm.MemReadValue<T>(_pid, addr, out var result, flags))
                throw new VmmException("Memory Read Failed!");
            return result;
        }

        /// <summary>
        /// Read value type/struct from specified address multiple times to ensure the read is correct.
        /// </summary>
        /// <typeparam name="T">Specified Value Type.</typeparam>
        /// <param name="addr">Address to read from.</param>
        public unsafe T ReadValueEnsure<T>(ulong addr)
            where T : unmanaged, allows ref struct
        {
            int cb = Unsafe.SizeOf<T>();
            if (!_vmm.MemReadValue<T>(_pid, addr, out var r1, VmmFlags.NOCACHE))
                throw new VmmException("Memory Read Failed!");
            Thread.SpinWait(5);
            if (!_vmm.MemReadValue<T>(_pid, addr, out var r2, VmmFlags.NOCACHE))
                throw new VmmException("Memory Read Failed!");
            Thread.SpinWait(5);
            if (!_vmm.MemReadValue<T>(_pid, addr, out var r3, VmmFlags.NOCACHE))
                throw new VmmException("Memory Read Failed!");
            var b1 = new ReadOnlySpan<byte>(&r1, cb);
            var b2 = new ReadOnlySpan<byte>(&r2, cb);
            var b3 = new ReadOnlySpan<byte>(&r3, cb);
            if (!b1.SequenceEqual(b2) || !b1.SequenceEqual(b3) || !b2.SequenceEqual(b3))
            {
                throw new VmmException("Memory Read Failed!");
            }
            return r1;
        }
        /// <summary>
        /// Read null terminated UTF8 string.
        /// </summary>
        public string ReadUtf8String(ulong addr, int cb, bool useCache = true) // read n bytes (string)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(cb, 0x1000, nameof(cb));
            var flags = useCache ? VmmFlags.NONE : VmmFlags.NOCACHE;
            return _vmm.MemReadString(_pid, addr, cb, Encoding.UTF8, flags) ??
                throw new VmmException("Memory Read Failed!");
        }

        /// <summary>
        /// Read null terminated Unicode string.
        /// </summary>
        public string ReadUnicodeString(ulong addr, int cb = 128, bool useCache = true)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(cb, 0x1000, nameof(cb));
            var flags = useCache ? VmmFlags.NONE : VmmFlags.NOCACHE;
            return _vmm.MemReadString(_pid, addr + 0x14, cb, Encoding.Unicode, flags) ??
                throw new VmmException("Memory Read Failed!");
        }
        /// <summary>
        /// Read null terminated string (utf-8/default).
        /// </summary>
        /// <param name="length">Number of bytes to read.</param>
        public string ReadString(ulong addr, int length, bool useCache = true)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, (int)0x1000, nameof(length));
            Span<byte> buffer = stackalloc byte[length];
            buffer.Clear();
            ReadSpan(addr, buffer, useCache); // ← CHANGED from ReadBuffer
            var nullIndex = buffer.IndexOf((byte)0);
            return nullIndex >= 0
                ? Encoding.UTF8.GetString(buffer.Slice(0, nullIndex))
                : Encoding.UTF8.GetString(buffer);
        }

        /// <summary>
        /// Read UnityEngineString structure
        /// </summary>
        public string ReadUnityString(ulong addr, int length = 64, bool useCache = true)
        {
            if (length % 2 != 0)
                length++;
            length *= 2; // Unicode 2 bytes per char
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, (int)0x1000, nameof(length));
            Span<byte> buffer = stackalloc byte[length];
            buffer.Clear();
            ReadSpan(addr + 0x14, buffer, useCache); // ← CHANGED from ReadBuffer
            var nullIndex = buffer.FindUtf16NullTerminatorIndex();
            return nullIndex >= 0
                ? Encoding.Unicode.GetString(buffer.Slice(0, nullIndex))
                : Encoding.Unicode.GetString(buffer);
        }
        #endregion
        #region Write Methods

        public void WriteValue<T>(LocalGameWorld game, ulong addr, T value) where T : unmanaged
        {
            //if (!game.IsSafeToWriteMem)
            //    throw new Exception("Not safe to write!");
            WriteValue(addr, value);
        }

        public void WriteValue<T>(ulong addr, T value) where T : unmanaged
        {
            if (!_vmm.MemWriteValue(_pid, addr, value))
                throw new VmmException("Memory Write Failed!");
        }
        public unsafe void WriteValueEnsure<T>(ulong addr, T value)
            where T : unmanaged
        {
            int cb = sizeof(T);
            try
            {
                var b1 = new ReadOnlySpan<byte>(&value, cb);
                const int retryCount = 3;
                for (int i = 0; i < retryCount; i++)
                {
                    try
                    {
                        WriteValue(addr, value);
                        Thread.SpinWait(5);
                        T temp = ReadValue<T>(addr, false);
                        var b2 = new ReadOnlySpan<byte>(&temp, cb);
                        if (b1.SequenceEqual(b2))
                        {
                            return; // SUCCESS
                        }
                    }
                    catch { }
                }
                throw new VmmException("Memory Write Failed!");
            }
            catch (VmmException)
            {
                throw;
            }
        }
        public unsafe void WriteBuffer<T>(ulong addr, Span<T> buffer)
            where T : unmanaged
        {
            //if (!MemWrites.Enabled)
            //    throw new Exception("Memory Writing is Disabled!");
            try
            {
                if (!_vmm.MemWriteSpan(_pid, addr, buffer))
                    throw new VmmException("Memory Write Failed!");
            }
            catch (VmmException)
            {
                throw;
            }
        }

        public void WriteBufferEnsure<T>(ulong addr, Span<T> buffer)
            where T : unmanaged
        {
            int cb = SizeChecker<T>.Size * buffer.Length;
            try
            {
                Span<byte> temp = cb > 0x1000 ? new byte[cb] : stackalloc byte[cb];
                ReadOnlySpan<byte> b1 = MemoryMarshal.Cast<T, byte>(buffer);
                const int retryCount = 3;
                for (int i = 0; i < retryCount; i++)
                {
                    try
                    {
                        WriteBuffer(addr, buffer);
                        Thread.SpinWait(5);
                        temp.Clear();
                        ReadSpan(addr, temp, false);
                        if (temp.SequenceEqual(b1))
                        {
                            return; // SUCCESS
                        }
                    }
                    catch { }
                }
                throw new VmmException("Memory Write Failed!");
            }
            catch (VmmException)
            {
                throw;
            }
        }

        #endregion
        #region Misc

        /// <summary>
        /// Creates a new <see cref="VmmScatterMap"/>.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VmmScatterMap CreateScatterMap() =>
            _vmm.CreateScatterMap(_pid);

        /// <summary>
        /// Creates a new <see cref="VmmScatter"/>.
        /// </summary>
        /// <param name="flags"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VmmScatter CreateScatter(VmmFlags flags = VmmFlags.NONE) =>
            _vmm.CreateScatter(_pid, flags);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong FindSignature(string signature)
        {

            if (!_vmm.Map_GetModuleFromName(_pid, "UnityPlayer.dll", out var info))
                throw new VmmException("Failed to get process information.");
            return _vmm.FindSignature(_pid, signature, info.vaBase, info.vaBase + info.cbImageSize);
        }

        /// <summary>
        /// Throws a special exception if no longer in game.
        /// </summary>
        /// <exception cref="OperationCanceledException"></exception>
        public void ThrowIfProcessNotRunning()
        {
            _vmm.ForceFullRefresh();
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    if (!_vmm.PidGetFromName(GAME_PROCESS_NAME, out uint pid))
                        throw new InvalidOperationException();
                    if (pid != _pid)
                        throw new InvalidOperationException();
                    return;
                }
                catch
                {
                    Thread.Sleep(150);
                }
            }

            throw new ProcessNotRunningException();
        }

        private sealed class ProcessNotRunningException : Exception
        {
            public ProcessNotRunningException()
                : base("Process is not running!")
            {
            }
        }

        #endregion

        #region Memory Macros

        /// <summary>
        /// Checks if a Virtual Address is valid.
        /// </summary>
        /// <param name="va">Virtual Address to validate.</param>
        /// <returns>True if valid, otherwise False.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidVirtualAddress(ulong va)
        {
            return va >= 0x10000 && ((long)va << 16) >> 16 == (long)va;
        }

        /// <summary>
        /// The PAGE_ALIGN macro returns a page-aligned virtual address for a given virtual address.
        /// https://learn.microsoft.com/windows-hardware/drivers/ddi/wdm/nf-wdm-page_align
        /// </summary>
        /// <param name="va">Virtual address.</param>
        /// <returns>Page-aligned virtual address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong PAGE_ALIGN(ulong va) => va & ~(0x1000ul - 1);

        /// <summary>
        /// The BYTE_OFFSET macro takes a virtual address and returns the byte offset of that address within the page.
        /// https://learn.microsoft.com/windows-hardware/drivers/ddi/wdm/nf-wdm-byte_offset
        /// </summary>
        /// <param name="va">virtual address.</param>
        /// <returns>Offset portion of the virtual address within the page.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint BYTE_OFFSET(ulong va) => (uint)(va & (0x1000ul - 1));

        /// <summary>
        /// The ADDRESS_AND_SIZE_TO_SPAN_PAGES macro returns the number of pages that a virtual range spans.
        /// The virtual range is defined by a virtual address and the size in bytes of a transfer request.
        /// https://learn.microsoft.com/windows-hardware/drivers/ddi/wdm/nf-wdm-address_and_size_to_span_pages
        /// </summary>
        /// <param name="va">Virtual address that is the base of the range.</param>
        /// <param name="size">Specifies the size in bytes.</param>
        /// <returns>Returns the number of pages spanned by the virtual range starting at Va.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ADDRESS_AND_SIZE_TO_SPAN_PAGES(ulong va, ulong size) =>
            (BYTE_OFFSET(va) + size + (0x1000ul - 1)) >> 12;

        /// <summary>
        /// Returns a length aligned to 8 bytes.
        /// Always rounds up.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint AlignLength(uint length) => (length + 7) & ~7u;

        /// <summary>
        /// Returns an address aligned to 8 bytes.
        /// Always the next aligned address.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong AlignAddress(ulong address) => (address + 7) & ~7ul;

        #endregion

        #region IDisposable

        private bool _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, true) == false)
            {
                _vmm.Dispose();
            }
        }

        #endregion
    }
}