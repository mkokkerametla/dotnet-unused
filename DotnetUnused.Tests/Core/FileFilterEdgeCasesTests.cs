using DotnetUnused.Core;
using Xunit;

namespace DotnetUnused.Tests.Core;

/// <summary>
/// Additional edge case tests for FileFilter to ensure robust filtering
/// </summary>
public class FileFilterEdgeCasesTests
{
    [Theory]
    [InlineData("C:\\MyProject\\src\\MyClass.cs", true)]
    [InlineData("C:/MyProject/src/MyClass.cs", true)]
    [InlineData("C:\\MyProject/mixed\\path\\MyClass.cs", true)]
    [InlineData("/unix/style/path/MyClass.cs", true)]
    public void ShouldAnalyze_HandlesMultiplePathSeparatorStyles(string filePath, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, FileFilter.ShouldAnalyze(filePath));
    }

    [Theory]
    [InlineData("C:\\bin\\MyClass.cs", false)] // bin at root
    [InlineData("C:\\obj\\MyClass.cs", false)] // obj at root
    [InlineData("bin\\MyClass.cs", false)] // relative path with bin
    [InlineData("obj\\MyClass.cs", false)] // relative path with obj
    [InlineData("C:\\project\\bin\\MyClass.cs", false)] // path contains \bin\
    [InlineData("C:\\project\\obj\\MyClass.cs", false)] // path contains \obj\
    [InlineData("/project/bin/MyClass.cs", false)] // Unix path contains /bin/
    [InlineData("/project/obj/MyClass.cs", false)] // Unix path contains /obj/
    public void ShouldAnalyze_ExcludesBinObjAtAnyLevel(string filePath, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, FileFilter.ShouldAnalyze(filePath));
    }

    [Theory]
    [InlineData("MyForm.designer.CS", false)] // Mixed case
    [InlineData("MyForm.DESIGNER.cs", false)] // All caps DESIGNER
    [InlineData("MyForm.Designer.CS", false)] // Mixed extension
    [InlineData("MyWindow.G.cs", false)] // Capital G
    [InlineData("MyWindow.g.CS", false)] // Capital CS
    public void ShouldAnalyze_CaseInsensitiveForGeneratedFiles(string filePath, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, FileFilter.ShouldAnalyze(filePath));
    }

    [Theory]
    [InlineData("C:\\Program Files\\MyApp\\src\\MyClass.cs", true)] // Space in path
    [InlineData("C:\\My Project (2024)\\src\\MyClass.cs", true)] // Parentheses and numbers
    [InlineData("C:\\Projects\\My-App_v2\\src\\MyClass.cs", true)] // Hyphens and underscores
    [InlineData("C:\\проект\\src\\MyClass.cs", true)] // Unicode characters
    public void ShouldAnalyze_HandlesSpecialCharactersInPath(string filePath, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, FileFilter.ShouldAnalyze(filePath));
    }

    [Theory]
    [InlineData("MyClass.cshtml", false)] // Razor files are not pure C# - should be excluded
    [InlineData("MyClass.razor", false)] // Blazor files are not pure C# - should be excluded
    [InlineData("MyClass.cs.bak", false)] // Backup files don't end with .cs - excluded
    [InlineData("MyClass.cs.old", false)] // Old files don't end with .cs - excluded
    [InlineData("MyClass.xaml", false)] // XAML files should be excluded
    [InlineData("MyClass.xaml.cs", true)] // XAML code-behind is C# and should be analyzed
    [InlineData("Program.cs", true)] // Regular C# files
    public void ShouldAnalyze_OnlyAllowsPureCSharpFiles(string filePath, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, FileFilter.ShouldAnalyze(filePath));
    }

    [Theory]
    [InlineData("C:\\MyProject\\SomeDesigner.cs", true)] // Ends with .cs - allowed
    [InlineData("C:\\MyProject\\MyClass.gcs", false)] // Doesn't end with .cs - excluded
    [InlineData("C:\\MyProject\\Test.Designer.txt", false)] // Doesn't end with .cs - excluded
    public void ShouldAnalyze_RequiresPreciseExtensionPatterns(string filePath, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, FileFilter.ShouldAnalyze(filePath));
    }

    [Theory]
    [InlineData("\\\\network\\share\\project\\bin\\MyClass.cs", false)]
    [InlineData("\\\\network\\share\\project\\src\\MyClass.cs", true)]
    [InlineData("\\\\server\\obj\\MyClass.cs", false)]
    public void ShouldAnalyze_HandlesUNCPaths(string filePath, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, FileFilter.ShouldAnalyze(filePath));
    }

    [Theory]
    [InlineData("C:\\MyProject\\binaries\\MyClass.cs", true)] // "bin" substring but not directory
    [InlineData("C:\\MyProject\\objects\\MyClass.cs", true)] // "obj" substring but not directory
    [InlineData("C:\\MyProject\\robin\\MyClass.cs", true)] // contains "bin"
    [InlineData("C:\\MyProject\\objection\\MyClass.cs", true)] // contains "obj"
    public void ShouldAnalyze_DoesNotExcludePartialMatches(string filePath, bool expected)
    {
        // Act & Assert - only exact "\\bin\\" or "\\obj\\" should be excluded
        Assert.Equal(expected, FileFilter.ShouldAnalyze(filePath));
    }

    [Fact]
    public void ShouldAnalyze_HandlesWhitespaceOnlyString()
    {
        // Arrange
        var filePath = "   ";

        // Act
        var result = FileFilter.ShouldAnalyze(filePath);

        // Assert - whitespace-only should be treated as invalid
        Assert.False(result);
    }

    [Theory]
    [InlineData("C:\\project\\bin\\MyClass.cs", false)] // File in bin directory
    [InlineData("C:\\project\\obj\\MyClass.cs", false)] // File in obj directory
    [InlineData("C:\\MyApp\\Debug\\bin\\output.cs", false)] // bin as subdirectory
    [InlineData("/home/user/project/obj/test.cs", false)] // obj directory in Unix path
    public void ShouldAnalyze_ExcludesBinObjAsTerminalDirectory(string filePath, bool expected)
    {
        // Paths ending with bin/obj directories should be excluded
        // Act & Assert
        Assert.Equal(expected, FileFilter.ShouldAnalyze(filePath));
    }

    [Theory]
    [InlineData("C:\\MyProject\\.Designer.cs", false)] // Starts with .Designer - generated file
    [InlineData("C:\\MyProject\\.g.cs", false)] // Starts with .g - generated file
    [InlineData("C:\\MyProject\\File.Designer.", false)] // Doesn't end with .cs - excluded
    [InlineData("C:\\MyProject\\File.cs", true)] // Regular .cs file - allowed
    public void ShouldAnalyze_HandlesEdgeCaseExtensions(string filePath, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, FileFilter.ShouldAnalyze(filePath));
    }

    [Theory]
    [InlineData("C:\\MyProject\\Migrations\\20231226120000_Initial.cs", false)]
    [InlineData("C:\\MyProject\\Migrations\\20240101000000_AddUsers.cs", false)]
    [InlineData("C:\\MyProject\\Migrations\\FoodDbContextModelSnapshot.cs", false)]
    [InlineData("C:\\MyProject\\Migrations\\Initial.cs", true)]
    [InlineData("C:\\MyProject\\Migrations\\Configuration.cs", true)]
    [InlineData("C:\\MyProject\\Data\\Migrations\\20231226120000_Init.cs", false)]
    [InlineData("/project/Migrations/20231226120000_Initial.cs", false)]
    public void ShouldAnalyze_HandlesEFCoreMigrations(string filePath, bool expected)
    {
        // EF Core migrations should be excluded, user files in Migrations folder allowed
        Assert.Equal(expected, FileFilter.ShouldAnalyze(filePath));
    }

    [Theory]
    [InlineData("C:\\MyProject\\MigrationsHelper.cs", true)]
    [InlineData("C:\\MyProject\\src\\20231226_Document.cs", true)]
    [InlineData("C:\\MyProject\\DataMigrations\\Script.cs", true)]
    public void ShouldAnalyze_OnlyExcludesEFMigrationsNotSimilarNames(
        string filePath,
        bool expected
    )
    {
        Assert.Equal(expected, FileFilter.ShouldAnalyze(filePath));
    }
}
