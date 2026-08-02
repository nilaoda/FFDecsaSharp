using FFDecsaSharp.Gui.ViewModels;

namespace FFDecsaSharp.Tests.Gui;

public sealed class TaskEditorViewModelTests
{
    [Fact]
    public void TryCreateTaskNormalizesSpacedControlWordBeforeValidation()
    {
        var viewModel = new TaskEditorViewModel
        {
            ControlWord = "20 24 05 49 02 AA AA 56",
        };
        viewModel.SetInputFiles([Path.Combine(Path.GetTempPath(), "input.ts")]);

        Assert.True(viewModel.TryCreateTask());
        Assert.Equal("2024054902AAAA56", viewModel.ControlWord);
        Assert.Equal("2024054902AAAA56", viewModel.Result?.EvenKey);
        Assert.Equal("2024054902AAAA56", viewModel.Result?.OddKey);
    }
}
