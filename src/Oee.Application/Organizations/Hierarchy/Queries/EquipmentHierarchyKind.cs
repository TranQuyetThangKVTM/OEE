namespace Oee.Application.Contracts.Modules;

/// <summary>Năm cấp của cây tổ chức, không bổ sung cấp con bên dưới WorkUnit.</summary>
public enum EquipmentHierarchyKind
{
    Enterprise,
    Site,
    Area,
    WorkCenter,
    WorkUnit
}
