using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PLangTests.Modules.UiModule;

[TestClass]
public class HtmlTest
{
    /*
     * in case I want to do this in future
     * wraps <plang_var> around variable. the idea it to update value react style more easily
     *

    [TestMethod]
    public void TestHtml()
    {
        var str = $@"<div class=""uk-accordion"" uk-accordion>
<h3 class=""uk-accordion-title"">
    {{{{ title }}}} - {{{{ subtitle }}}}
</h3>
<div class=""uk-accordion-content"" id=""formattedInterview"">
    {{{{ ChildElement1 }}}}
    {{{{ ChildElement2 }}}}
    {{{{ ChildElement3 }}}}
</div>
</div>";


        var wrapper = new PLang.Modules.UiModule.PlangVarHtmlWrapper();
        string newHtml = wrapper.WrapHtml(str);
        int i = 0;

        string expectedHtml = @"<div class=""uk-accordion"" uk-accordion>
<h3 class=""uk-accordion-title"" data-variable=""title, subtitle"">
    <plang_var name=""title"">{{ title }}</plang_var> - <plang_var name=""subtitle"">{{ subtitle }}</plang_var>
</h3>
<div class=""uk-accordion-content"" id=""formattedInterview"">
    {{ ChildElement1 }}
    {{ ChildElement2 }}
    {{ ChildElement3 }}
</div>
</div>";

        Assert.AreEqual(expectedHtml, newHtml);
    }

    [TestMethod]
    public void TestHtmlWithFor()
    {
        var str = $@"
<div class=""uk-accordion"" uk-accordion>
<h3 class=""uk-accordion-title"">Hrátt viðtal</h3>
<div class=""uk-accordion-content"" {{{{ if id == null }}}} style=""display: none;"" {{{{ end }}}}>
    {{{{ ChildrenElement0 }}}}
</div>
</div>


<h3 class=""uk-accordion-title"">
    {{{{ title }}}} - {{{{ subtitle }}}}
</h3>
<div class=""uk-accordion-content"" id=""formattedInterview"">
    {{{{ ChildElement1 }}}}
    {{{{ ChildElement2 }}}}
    {{{{ ChildElement3 }}}}
</div>
<table class=""uk-table uk-table-divider uk-table-hover"">
    <thead>
        <tr>
            <th>Name</th>
            <th>Kennitala</th>
            <th>Created</th>
            <th>Actions</th>
        </tr>
    </thead>
    <tbody>
        {{{{ for interview in interviews }}}}
        <tr>
            <td>{{{{ interview.name }}}}</td>
            <td>{{{{ interview.kennitala }}}}</td>
            <td>{{{{ interview.created | date.format ""yyyy-MM-dd"" }}}}</td>
            <td>
                <a href=""/view/{{{{ interview.id }}}}"" class=""uk-icon-link"" uk-icon=""icon: file-text""></a>
                <a href=""/EditInterview/{{{{ interview.id }}}}"" class=""uk-icon-link"" uk-icon=""icon: pencil""></a>
{{{{ user.fullName }}}}
            </td>
        </tr>
        {{{{ end }}}}

    </tbody>
</table>";


        var wrapper = new PLang.Modules.UiModule.PlangVarHtmlWrapper();
        string newHtml = wrapper.WrapHtml(str);
        int i = 0;

        string expectedHtml = @$"
<div class=""uk-accordion"" uk-accordion>
<h3 class=""uk-accordion-title"">Hrátt viðtal</h3>
<div class=""uk-accordion-content"" {{{{ if id == null }}}} style=""display: none;"" {{{{ end }}}}>
    {{{{ ChildrenElement0 }}}}
</div>
</div>


<h3 class=""uk-accordion-title"">
    <plang_var name=""title"">{{{{ title }}}}</plang_var> - <plang_var name=""subtitle"">{{{{ subtitle }}}}</plang_var>
</h3>
<div class=""uk-accordion-content"" id=""formattedInterview"">
    {{ ChildrenElement1 }}
    {{ ChildrenElement2 }}
    {{ ChildrenElement3 }}
</div>
<table class=""uk-table uk-table-divider uk-table-hover"">
    <thead>
        <tr>
            <th>Name</th>
            <th>Kennitala</th>
            <th>Created</th>
            <th>Actions</th>
        </tr>
    </thead>
    <tbody>
        <plang_var name=""interview"">
{{{{ for interview in interviews}}}}
        <tr>
            <td>{{{{ interview.name }}}}</td>
            <td>{{{{ interview.kennitala }}}}</td>
            <td>interview.created|date.format ""yyyy-MM-dd""</td>
            <td>
                <a href=""/view/{{{{ interview.id }}}}"" class=""uk-icon-link"" uk-icon=""icon: file-text""></a>
                <a href=""/EditInterview/{{{{ interview.id }}}}"" class=""uk-icon-link"" uk-icon=""icon: pencil""></a>
<plang_var name=""user.fullName"">{{{{ user.fullName }}}}</plang_var>
            </td>
        </tr>
        {{{{ end }}}}
</plang_var>

    </tbody>
</table>";

        Assert.AreEqual(expectedHtml, newHtml);

    } */
}