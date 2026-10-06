using System.Collections.Generic;

namespace ReVibranceGUI.Scanners
{
    public interface IGameScanner
    {
        IEnumerable<GameInstall> Scan();
    }
}
