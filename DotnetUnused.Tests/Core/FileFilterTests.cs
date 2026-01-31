using DotnetUnused.Core;
using Xunit;

namespace DotnetUnused.Tests.Core;

public class FileFilterTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("C:\\project\\src\\MyClass.cs", true)]
    [InlineData("C:\\project\\bin\\Debug\\MyClass.cs", false)]
    [InlineData("C:\\project\\obj\\Debug\\MyClass.cs", false)]
    [InlineData("/project/bin/Debug/MyClass.cs", false)]
    [InlineData("/project/obj/Debug/MyClass.cs", false)]
    [InlineData("C:\\project\\src\\Form.Designer.cs", false)]
    [InlineData("C:\\project\\src\\MainWindow.g.cs", false)]
    [InlineData("C:\\project\\src\\MainWindow.g.i.cs", false)]
    [InlineData("C:\\project\\src\\Resource.Designer.cs", false)]
    public void ShouldAnalyze_ReturnsExpectedResult(string? filePath, bool expected)
    {
        // Act
        var result = FileFilter.ShouldAnalyze(filePath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldAnalyze_ExcludesBinDirectory()
    {
        // Arrange
        var filePath = "C:\\MyProject\\bin\\Release\\net8.0\\MyClass.cs";

        // Act
        var result = FileFilter.ShouldAnalyze(filePath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldAnalyze_ExcludesObjDirectory()
    {
        // Arrange
        var filePath = "C:\\MyProject\\obj\\Release\\net8.0\\MyClass.g.cs";

        // Act
        var result = FileFilter.ShouldAnalyze(filePath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldAnalyze_AllowsRegularSourceFiles()
    {
        // Arrange
        var filePath = "C:\\MyProject\\src\\Controllers\\HomeController.cs";

        // Act
        var result = FileFilter.ShouldAnalyze(filePath);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("C:\\MyProject\\bin\\MyClass.cs")]
    [InlineData("C:\\MyProject\\Debug\\bin\\MyClass.cs")]
    [InlineData("C:\\MyProject\\src\\bin\\MyClass.cs")]
    [InlineData("/home/user/project/bin/MyClass.cs")]
    [InlineData("/home/user/project/obj/MyClass.cs")]
    public void ShouldAnalyze_ExcludesBinObjInAnyLocation(string filePath)
    {
        // Act & Assert
        Assert.False(FileFilter.ShouldAnalyze(filePath));
    }

    [Theory]
    [InlineData("Form1.Designer.cs")]
    [InlineData("C:\\MyProject\\MainWindow.g.cs")]
    [InlineData("C:\\MyProject\\App.g.i.cs")]
    [InlineData("C:\\MyProject\\Resources.Designer.cs")]
    [InlineData("/project/View.Designer.cs")]
    public void ShouldAnalyze_ExcludesGeneratedFiles(string filePath)
    {
        // Act & Assert
        Assert.False(FileFilter.ShouldAnalyze(filePath));
    }

    [Theory]
    [InlineData("MyClass.cs")]
    [InlineData("C:\\MyProject\\Services\\AuthService.cs")]
    [InlineData("/home/user/src/Models/User.cs")]
    [InlineData("C:\\MyProject\\Tests\\UnitTest1.cs")]
    [InlineData("C:\\MyProject\\Migrations\\Initial.cs")]
    public void ShouldAnalyze_AllowsVariousValidPaths(string filePath)
    {
        // Act & Assert
        Assert.True(FileFilter.ShouldAnalyze(filePath));
    }

    [Theory]
    [InlineData("C:\\Projects\\MyApp\\bin\\Release\\net8.0\\MyApp.dll.cs")]
    [InlineData("C:\\Projects\\MyApp\\obj\\x64\\Debug\\TemporaryGeneratedFile.cs")]
    [InlineData("C:/Projects/MyApp/bin/Debug/netcoreapp3.1/MyApp.Views.cs")]
    public void ShouldAnalyze_ExcludesDeepBinObjPaths(string filePath)
    {
        // Act & Assert
        Assert.False(FileFilter.ShouldAnalyze(filePath));
    }

    [Fact]
    public void ShouldAnalyze_HandlesCaseSensitivity()
    {
        // Designer.cs check is case-insensitive
        Assert.False(FileFilter.ShouldAnalyze("Form1.DESIGNER.CS"));
        Assert.False(FileFilter.ShouldAnalyze("Form1.designer.cs"));

        // g.cs check is case-insensitive
        Assert.False(FileFilter.ShouldAnalyze("MainWindow.G.CS"));
        Assert.False(FileFilter.ShouldAnalyze("MainWindow.G.I.CS"));
    }

    [Theory]
    [InlineData("C:\\MyProject\\Binaries\\MyClass.cs", true)] // "bin" in folder name but not "/bin/"
    [InlineData("C:\\MyProject\\Objects\\MyClass.cs", true)] // "obj" in folder name but not "/obj/"
    [InlineData("C:\\MyProject\\combine\\MyClass.cs", true)] // contains "bin" but not as directory
    public void ShouldAnalyze_OnlyExcludesExactBinObjDirectories(string filePath, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, FileFilter.ShouldAnalyze(filePath));
    }

    [Theory]
    [InlineData("MyDesigner.cs", true)] // Contains "Designer" but ends with .cs - allowed
    [InlineData("DesignerHelpers.cs", true)] // Contains "Designer" but not .Designer.cs pattern
    [InlineData("Program.g.cs.backup", false)] // Doesn't end with .cs - excluded
    [InlineData("Program.cs", true)] // Regular .cs file - allowed
    public void ShouldAnalyze_RequiresExactFileExtensionMatch(string filePath, bool expected)
    {
        // Act & Assert
        Assert.Equal(expected, FileFilter.ShouldAnalyze(filePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void ShouldAnalyze_HandlesInvalidInput_Safely(string? filePath)
    {
        // Act & Assert - should not throw, just return false
        Assert.False(FileFilter.ShouldAnalyze(filePath));
    }
}
