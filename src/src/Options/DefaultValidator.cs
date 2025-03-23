using FluentValidation;

namespace Femur;

internal class DefaultValidator<TOptions> : AbstractValidator<TOptions>
    where TOptions : class, IStandardOptions<TOptions>
{
    public DefaultValidator(Action<AbstractValidator<TOptions>> setup)
    {
        setup(this);
    }
}