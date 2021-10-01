namespace ThemesOfDotNet.Indexing.Validation;

public abstract class ValidationRule
{
    public abstract void Validate(ValidationContext context);
}
