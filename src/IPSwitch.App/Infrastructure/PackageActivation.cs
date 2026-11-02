using System.Runtime.InteropServices;
using IPSwitch.Core.Diagnostics;
using IPSwitch.Core.Storage;

namespace IPSwitch.App.Infrastructure;

/// <summary>
/// Erkennt, ob Windows das Store-Paket als Autostart (StartupTask) gestartet hat. Ein StartupTask übergibt keine
/// Befehlszeilenargumente; die Art des Starts liefert nur AppInstance.GetActivatedEventArgs(). Der Aufruf erfolgt
/// direkt über die WinRT-ABI, damit keine zusätzlichen Windows-SDK-Bibliotheken ausgeliefert werden müssen.
/// </summary>
internal static class PackageActivation
{
    private const string AppInstanceClass = "Windows.ApplicationModel.AppInstance";
    private const int ActivationKindStartupTask = 1020;

    // IInspectable belegt die ersten sechs Einträge der Methodentabelle (IUnknown: 3, IInspectable: 3).
    private const int FirstInterfaceMethod = 6;

    private static readonly Guid IAppInstanceStatics = new("9D11E77F-9EA6-47AF-A6EC-46784C5BA254");

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetActivatedEventArgs(IntPtr self, out IntPtr args);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetKind(IntPtr self, out int kind);

    public static bool IsStartupTask()
    {
        if (!AppPackage.IsPackaged)
        {
            return false;
        }

        IntPtr className = IntPtr.Zero, statics = IntPtr.Zero, args = IntPtr.Zero;
        try
        {
            if (WindowsCreateString(AppInstanceClass, AppInstanceClass.Length, out className) != 0)
            {
                return false;
            }

            var iid = IAppInstanceStatics;
            if (RoGetActivationFactory(className, ref iid, out statics) != 0 || statics == IntPtr.Zero)
            {
                return false;
            }

            // IAppInstanceStatics: get_RecommendedInstance, GetActivatedEventArgs, ...
            var getArgs = Marshal.GetDelegateForFunctionPointer<GetActivatedEventArgs>(Method(statics, 1));
            if (getArgs(statics, out args) != 0 || args == IntPtr.Zero)
            {
                return false;
            }

            // IActivatedEventArgs: get_Kind, get_PreviousExecutionState, get_SplashScreen
            var getKind = Marshal.GetDelegateForFunctionPointer<GetKind>(Method(args, 0));
            if (getKind(args, out var kind) != 0)
            {
                return false;
            }

            Log.Info($"Paketstart, ActivationKind {kind}.");
            return kind == ActivationKindStartupTask;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or COMException)
        {
            Log.Warn("Startart des Pakets nicht ermittelbar: " + ex.Message);
            return false;
        }
        finally
        {
            if (args != IntPtr.Zero)
            {
                Marshal.Release(args);
            }

            if (statics != IntPtr.Zero)
            {
                Marshal.Release(statics);
            }

            if (className != IntPtr.Zero)
            {
                WindowsDeleteString(className);
            }
        }
    }

    private static IntPtr Method(IntPtr instance, int index) =>
        Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), (FirstInterfaceMethod + index) * IntPtr.Size);

    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string sourceString, int length, out IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int RoGetActivationFactory(IntPtr activatableClassId, ref Guid iid, out IntPtr factory);
}
