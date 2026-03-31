namespace SIS.Application.Scheduling.DTOs;

public class SwapSectionsRequestDto
{
    public int SectionId1 { get; set; }
    public int SectionId2 { get; set; }
    public bool SwapSlots { get; set; }
    public bool SwapClassrooms { get; set; }
}
