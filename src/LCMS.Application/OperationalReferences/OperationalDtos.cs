namespace LCMS.Application.OperationalReferences;

/// <summary>Bill shown on operational-reference relationship views (AC-SCP-06).</summary>
public sealed record OperationalBillRef(Guid Id, string BillNo, string OperationalStatus);
