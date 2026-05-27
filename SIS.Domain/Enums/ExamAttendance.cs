namespace SIS.Domain.Enums;

public enum ExamAttendance
{
    NotMarked = 0, // Henüz işaretlenmedi (varsayılan)
    Present   = 1, // Sınava girdi
    Absent    = 2, // Mazeretsiz girmedi → o sınav 0 sayılır
    Excused   = 3  // Mazeretli girmedi → mazeret bekliyor
}
