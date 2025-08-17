using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "UNT0026:GetComponent always allocates / Use TryGetComponent", Justification = "TryGetComponent is broken in GTFO Il2Cpp Environment")]
[assembly: SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "GTFO is a windows-only game.")]