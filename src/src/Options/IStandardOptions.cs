using FluentValidation;

namespace Femur;

public interface IStandardOptions<TOptions> where TOptions : class, IStandardOptions<TOptions>
{
    static abstract string SectionName { get; }
    static abstract void SetupValidator(AbstractValidator<TOptions> validator);
}