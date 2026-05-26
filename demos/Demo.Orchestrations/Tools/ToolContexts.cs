using Microsoft.Maui.AI.Attributes;

namespace Demo.Orchestrations.Tools;

[AIToolSource(typeof(NewsDeskTools))]
public partial class NewsDeskToolContext : AIToolContext { }

[AIToolSource(typeof(HelpDeskTools))]
public partial class HelpDeskToolContext : AIToolContext { }

[AIToolSource(typeof(TravelTools))]
public partial class TravelToolContext : AIToolContext { }

[AIToolSource(typeof(StartupTools))]
public partial class StartupToolContext : AIToolContext { }
