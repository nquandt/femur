using FluentValidation;

namespace Femur.Options;

internal sealed class DefaultValidator<TOptions> : AbstractValidator<TOptions>
    where TOptions : class, IStandardOptions<TOptions>
{
    public DefaultValidator(Action<AbstractValidator<TOptions>> setup)
    {
        setup(this);
    }
}