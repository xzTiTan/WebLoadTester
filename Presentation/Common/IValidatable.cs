using System.Collections.Generic;

namespace WebLoadTester.Presentation.Common;

public interface IValidatable
{
    IReadOnlyList<string> Validate();
}
