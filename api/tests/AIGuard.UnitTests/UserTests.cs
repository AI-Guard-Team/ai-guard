using AIGuard.Domain.Entities;

namespace AIGuard.UnitTests;

public class UserTests
{
    [Theory]
    [InlineData("Ali@Example.COM", "ali@example.com")]
    [InlineData("  ali@example.com  ", "ali@example.com")]
    public void NormalizeEmail_is_trimmed_and_lowercase_invariant(string input, string expected)
    {
        Assert.Equal(expected, User.NormalizeEmail(input));
    }

    [Fact]
    public void NormalizeEmail_does_not_depend_on_current_culture()
    {
        // Türkçe kültürde "I".ToLower() = "ı" olur; e-posta karşılaştırmasında bu, aynı
        // adresin iki farklı hesap gibi görünmesine yol açardı.
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            Assert.Equal("ilker@x.com", User.NormalizeEmail("ILKER@X.COM"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
