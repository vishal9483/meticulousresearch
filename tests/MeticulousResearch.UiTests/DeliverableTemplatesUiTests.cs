using FlaUI.Core.AutomationElements;

namespace MeticulousResearch.UiTests;

/// <summary>
/// @ui scenarios from docs/features/deliverable-templates/tests.md (SPEC §3.4.1). These drive the
/// real WPF window via FlaUI (UIA3) and require a desktop session, so they are tagged
/// <c>Category=ui</c> and excluded from the headless gate; they must compile and build. They assert
/// the deliverable-template gallery (name / description / preview) is surfaced in the New-artifact
/// and New-project flows.
/// </summary>
[Trait("Category", "ui")]
[Collection("shell-ui")]
public sealed class DeliverableTemplatesUiTests
{
    private readonly ShellUiFixture _fixture;

    public DeliverableTemplatesUiTests(ShellUiFixture fixture) => _fixture = fixture;

    // Scenario: The New-artifact flow surfaces the template gallery with previews
    //   Given the "New artifact" flow is open
    //   Then the template gallery is shown with each template's name, description, and a preview
    [Fact]
    public void The_New_artifact_flow_surfaces_the_template_gallery_with_previews()
    {
        var window = _fixture.MainWindow;

        // Open the New-artifact flow.
        var newArtifact = window.FindFirstDescendant(cf => cf.ByAutomationId("NewArtifactButton"))?.AsButton()
            ?? throw new NotSupportedException(
                "The New-artifact flow entry point is owned by artifact-creation; wire this test to it when available.");
        newArtifact.Click();

        // The template gallery is shown.
        var gallery = window.FindFirstDescendant(cf => cf.ByAutomationId("TemplateGallery"));
        Assert.NotNull(gallery);

        // ...with each template's name, description, and a preview.
        var item = gallery!.FindFirstDescendant(cf => cf.ByAutomationId("TemplateGalleryItem"));
        Assert.NotNull(item);
        Assert.NotNull(item!.FindFirstDescendant(cf => cf.ByAutomationId("TemplateName")));
        Assert.NotNull(item.FindFirstDescendant(cf => cf.ByAutomationId("TemplateDescription")));
        Assert.NotNull(item.FindFirstDescendant(cf => cf.ByAutomationId("TemplatePreview")));
    }

    // Scenario: The New-project flow surfaces the template gallery
    //   Given the Projects home is open
    //   When I choose "New project"
    //   Then the template gallery is shown so I can start a project from a template
    [Fact]
    public void The_New_project_flow_surfaces_the_template_gallery()
    {
        var window = _fixture.MainWindow;

        // When I choose "New project" from the Projects home.
        var newProject = window.FindFirstDescendant(cf => cf.ByName("New project"))?.AsButton()
            ?? window.FindFirstDescendant(cf => cf.ByAutomationId("NewProjectButton"))?.AsButton()
            ?? throw new NotSupportedException(
                "The New-project flow entry point is owned by projects-crud; wire this test to it when available.");
        newProject.Click();

        // Then the template gallery is shown so I can start a project from a template.
        var gallery = window.FindFirstDescendant(cf => cf.ByAutomationId("TemplateGallery"));
        Assert.NotNull(gallery);
    }
}
