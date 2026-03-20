// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// Windows Forms specific suppressions
[assembly: SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Windows-only application")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "UI requires broad exception handling")]
[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Application is not localized")]
[assembly: SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Not used for security purposes")]
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Windows Forms manages control lifetime")]
[assembly: SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Controls are disposed by parent form")]
[assembly: SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Internal methods with controlled usage")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance methods for consistency")]
[assembly: SuppressMessage("Design", "CA1063:Implement IDisposable Correctly", Justification = "Base Form class handles disposal")]
