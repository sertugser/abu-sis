namespace SIS.Application.Scheduling.Constraints;

public interface IConstraintEvaluator
{
    string Code { get; }
    string Name { get; }
    ConstraintCategory Category { get; }
    ConstraintResult Evaluate(IConstraintContext context);
}

public enum ConstraintCategory
{
    Hard,
    Soft,
}
