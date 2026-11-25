using System.Diagnostics.CodeAnalysis;

// These public names predate the code-behind split. Renaming them would break component consumers.
[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
    Justification = "Preserve the existing public Razor component namespace.",
    Scope = "namespace", Target = "~N:Oee.Web.Components.Shared")]
[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
    Justification = "Preserve the existing public Error component type and its consumers.",
    Scope = "type", Target = "~T:Oee.Web.Components.Pages.Error")]
