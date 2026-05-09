namespace SIS.Domain.Enums;

public static class ClassroomTypeExtensions
{
    public static bool IsLabCategory(this ClassroomType type) => type switch
    {
        ClassroomType.Lab
            or ClassroomType.Computer
            or ClassroomType.PhysicsLab
            or ClassroomType.ChemistryLab
            or ClassroomType.BiologyLab
            or ClassroomType.ElectronicsLab
            or ClassroomType.DesignStudio => true,
        _ => false,
    };
}
