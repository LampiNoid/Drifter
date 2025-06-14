using Robust.Client.UserInterface.RichText;

namespace Content.Client._Finster.UserInterface.RichText;

public sealed class ExamineBorderTag : IMarkupTag
{
    [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;

    public const string TagName = "examineborder";

    public string Name => TagName;
}
