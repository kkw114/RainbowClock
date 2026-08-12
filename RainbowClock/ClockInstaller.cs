using Zenject;

namespace RainbowClock
{
    public class ClockInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesTo<ClockController>().AsSingle();
        }
    }
}
