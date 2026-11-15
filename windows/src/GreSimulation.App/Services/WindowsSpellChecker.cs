using System.Runtime.InteropServices;
using GreSimulation.Core;

namespace GreSimulation.App.Services;

/// <summary>
/// Counts misspelled words with the Windows Spell Checking API (ISpellCheckerFactory → ISpellChecker,
/// "en-US"), the Windows counterpart of the Mac app's NSSpellChecker. The COM interfaces are called
/// through their vtables, so this needs no generated interop. Repeated-word warnings are not counted,
/// because NSSpellChecker does not report them. If the API is unavailable, nothing is counted.
/// </summary>
public sealed class WindowsSpellChecker : ISpellChecker
{
    private const string Language = "en-US";
    private static readonly Guid FactoryClassId = new("7AB36653-1796-484B-BDFA-E74F1DB7C1DC");
    private static readonly Guid FactoryInterfaceId = new("8E018A9D-2415-4677-BF08-794EA61F94BB");
    private const uint ClassContextInprocServer = 0x1;
    private const int NotInitialized = unchecked((int)0x800401F0);
    private const int CorrectiveActionDelete = 3;

    private WindowsSpellChecker() { }

    /// <summary>Returns a checker when the Windows spell checker supports en-US, otherwise null.</summary>
    public static WindowsSpellChecker? TryCreate()
    {
        try
        {
            var checker = OpenChecker();
            if (checker == IntPtr.Zero) return null;
            Marshal.Release(checker);
            return new WindowsSpellChecker();
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
            return null;
        }
    }

    public int CountMisspellings(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var checker = IntPtr.Zero;
        var errors = IntPtr.Zero;
        try
        {
            // Created on the calling thread so the COM object always lives in the caller's apartment.
            checker = OpenChecker();
            if (checker == IntPtr.Zero) return 0;
            var check = Method<CheckFn>(checker, 4); // ISpellChecker::Check
            if (check(checker, text, out errors) < 0 || errors == IntPtr.Zero) return 0;
            var next = Method<NextFn>(errors, 3);     // IEnumSpellingError::Next
            var count = 0;
            while (next(errors, out var error) == 0 && error != IntPtr.Zero)
            {
                try
                {
                    var action = Method<GetIntFn>(error, 5); // ISpellingError::get_CorrectiveAction
                    if (action(error, out var corrective) < 0 || corrective != CorrectiveActionDelete) count += 1;
                }
                finally
                {
                    Marshal.Release(error);
                }
            }
            return count;
        }
        catch (Exception exception)
        {
            CrashLog.Write(exception);
            return 0;
        }
        finally
        {
            if (errors != IntPtr.Zero) Marshal.Release(errors);
            if (checker != IntPtr.Zero) Marshal.Release(checker);
        }
    }

    /// <summary>Creates an en-US ISpellChecker (caller releases it), or returns zero when unsupported.</summary>
    private static IntPtr OpenChecker()
    {
        var classId = FactoryClassId;
        var interfaceId = FactoryInterfaceId;
        var hr = CoCreateInstance(ref classId, IntPtr.Zero, ClassContextInprocServer, ref interfaceId, out var factory);
        if (hr == NotInitialized)
        {
            CoInitializeEx(IntPtr.Zero, 0); // COINIT_MULTITHREADED for a thread that never initialized COM
            hr = CoCreateInstance(ref classId, IntPtr.Zero, ClassContextInprocServer, ref interfaceId, out factory);
        }
        if (hr < 0 || factory == IntPtr.Zero) return IntPtr.Zero;
        try
        {
            var isSupported = Method<IsSupportedFn>(factory, 4); // ISpellCheckerFactory::IsSupported
            if (isSupported(factory, Language, out var supported) < 0 || supported == 0) return IntPtr.Zero;
            var create = Method<CreateSpellCheckerFn>(factory, 5); // ISpellCheckerFactory::CreateSpellChecker
            return create(factory, Language, out var checker) < 0 ? IntPtr.Zero : checker;
        }
        finally
        {
            Marshal.Release(factory);
        }
    }

    private static T Method<T>(IntPtr comObject, int slot) where T : Delegate
    {
        var table = Marshal.ReadIntPtr(comObject);
        return Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(table, slot * IntPtr.Size));
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int IsSupportedFn(IntPtr self, [MarshalAs(UnmanagedType.LPWStr)] string languageTag, out int value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateSpellCheckerFn(IntPtr self, [MarshalAs(UnmanagedType.LPWStr)] string languageTag, out IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CheckFn(IntPtr self, [MarshalAs(UnmanagedType.LPWStr)] string text, out IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int NextFn(IntPtr self, out IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetIntFn(IntPtr self, out int value);

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(ref Guid classId, IntPtr outer, uint context, ref Guid interfaceId, out IntPtr result);

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr reserved, uint coInit);
}
