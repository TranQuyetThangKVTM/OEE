namespace Oee.Application.Production.Automation;

public sealed record AutomaticShiftPdf(
    byte[] Content,
    string FileName);
