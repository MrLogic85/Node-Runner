using System.Runtime.CompilerServices;
using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests;

/// <summary>
/// Unlocks the Wheel for the whole assembly before any test runs (#129), so no test toggles shared
/// state. The Ui tests run in their own process with the Wheel locked, as on main. Remove this with
/// the gate when the Wheel unlocks (#1087).
/// </summary>
internal static class UnreleasedParts
{
    [ModuleInitializer]
    internal static void Unlock() => PartTray.UnreleasedPartsUnlocked = true;
}
