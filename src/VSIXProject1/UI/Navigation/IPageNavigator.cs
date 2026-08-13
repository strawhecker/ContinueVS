#nullable enable

using System;
using System.Windows.Controls;
using System.Threading.Tasks;

namespace ContinueVS.UI.Navigation
{
    public interface IPageNavigator
    {
        Task NavigateAsync(string? route, Frame? frame);
    }
}
