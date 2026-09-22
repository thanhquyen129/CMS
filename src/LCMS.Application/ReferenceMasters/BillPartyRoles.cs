using LCMS.Application.BusinessParties;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;

namespace LCMS.Application.ReferenceMasters;

/// <summary>Shared Bill party-role checks and snapshot capture.</summary>
public static class BillPartyRoles
{
    public static readonly IReadOnlySet<string> Assignable = new HashSet<string>(StringComparer.Ordinal)
    {
        PartyRoleCodes.Customer,
        PartyRoleCodes.Payer,
        PartyRoleCodes.Shipper,
        PartyRoleCodes.Consignee,
        PartyRoleCodes.BillTo
    };

    public static Task EnsureRequiredAsync(Bill bill, BillPartyPolicy policy, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        foreach (var role in policy.RequiredRoles)
        {
            if (PartyId(bill, role) is null)
            {
                throw new ConflictAppException($"Bill thiếu vai trò bắt buộc: {role}.");
            }
        }

        return Task.CompletedTask;
    }

    public static async Task CaptureAsync(
        Bill bill,
        IPartyDirectoryService parties,
        IPartySnapshotCapture snapshots,
        CancellationToken cancellationToken)
    {
        await CaptureOne(bill, parties, snapshots, PartyRoleCodes.Customer, bill.CustomerPartyId, "gắn khách hàng lên Bill", cancellationToken);
        await CaptureOne(bill, parties, snapshots, PartyRoleCodes.Payer, bill.PayerPartyId, "gắn bên trả tiền lên Bill", cancellationToken);
        await CaptureOne(bill, parties, snapshots, PartyRoleCodes.Shipper, bill.ShipperPartyId, "gắn người gửi lên Bill", cancellationToken);
        await CaptureOne(bill, parties, snapshots, PartyRoleCodes.Consignee, bill.ConsigneePartyId, "gắn người nhận lên Bill", cancellationToken);
        await CaptureOne(bill, parties, snapshots, PartyRoleCodes.BillTo, bill.BillToPartyId, "gắn bên nhận hóa đơn lên Bill", cancellationToken);
    }

    private static async Task CaptureOne(
        Bill bill,
        IPartyDirectoryService parties,
        IPartySnapshotCapture snapshots,
        string role,
        Guid? partyId,
        string purpose,
        CancellationToken cancellationToken)
    {
        if (partyId is not Guid id)
        {
            return;
        }

        await parties.EnsureUsableAsync(id, [role], purpose, cancellationToken);
        await snapshots.CapturePartyAsync(PartySnapshotObjectTypes.Bill, bill.Id, role, id, cancellationToken);
    }

    private static Guid? PartyId(Bill bill, string role) => role switch
    {
        PartyRoleCodes.Customer => bill.CustomerPartyId,
        PartyRoleCodes.Payer => bill.PayerPartyId,
        PartyRoleCodes.Shipper => bill.ShipperPartyId,
        PartyRoleCodes.Consignee => bill.ConsigneePartyId,
        PartyRoleCodes.BillTo => bill.BillToPartyId,
        _ => null
    };
}
