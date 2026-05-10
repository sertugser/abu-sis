namespace SIS.Application.Scheduling.Solver;

/// <summary>
/// Bir section atamasının hangi bileşenine ait olduğunu belirtir.
/// Solver-specific kavram — Domain'deki SessionType (attendance) ile karıştırılmamalı.
/// </summary>
public enum ComponentType
{
    Lecture,
    Lab
}
