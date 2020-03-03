using System.Collections.Generic;
using Content.Server.GameObjects.Components.Research;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Timers;
using Robust.Shared.IoC;
using Robust.Shared.Timers;

namespace Content.Server.GameObjects.EntitySystems
{
    public class ResearchSystem : EntitySystem
    {
        public const int ResearchConsoleUIUpdateTimeMs = 30000;

        private Timer _timer;

        private readonly List<ResearchServerComponent> _servers = new List<ResearchServerComponent>();
        private readonly IEntityQuery ConsoleQuery;
        public IReadOnlyList<ResearchServerComponent> Servers => _servers;

        public ResearchSystem()
        {
            ConsoleQuery = new TypeEntityQuery(typeof(ResearchConsoleComponent));
            _timer = new Timer(ResearchConsoleUIUpdateTimeMs, true, OnConsoleUIUpdate);
            IoCManager.Resolve<ITimerManager>().AddTimer(_timer);
        }

        public bool RegisterServer(ResearchServerComponent server)
        {
            if (_servers.Contains(server)) return false;
            _servers.Add(server);
            return true;
        }

        public void UnregisterServer(ResearchServerComponent server)
        {
            _servers.Remove(server);
        }

        public ResearchServerComponent GetServerById(int id)
        {
            foreach (var server in Servers)
            {
                if (server.Id == id) return server;
            }

            return null;
        }

        public string[] GetServerNames()
        {
            var list = new string[Servers.Count];

            for (var i = 0; i < Servers.Count; i++)
            {
                list[i] = Servers[i].ServerName;
            }

            return list;
        }

        public int[] GetServerIds()
        {
            var list = new int[Servers.Count];

            for (var i = 0; i < Servers.Count; i++)
            {
                list[i] = Servers[i].Id;
            }

            return list;
        }

        private void OnConsoleUIUpdate()
        {
            foreach (var console in EntityManager.GetEntities(ConsoleQuery))
            {
                console.GetComponent<ResearchConsoleComponent>().UpdateUserInterface();
            }
        }
    }
}
