﻿using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Client.Interfaces.GameObjects.Components;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Client.GameObjects.EntitySystems
{
    public sealed partial class VerbSystem
    {
        public const int GroupingTypes = 2;
        private static int GroupingContextMenuType { get; set; }

        private void OnGroupingContextMenuChanged(int obj)
        {
            CloseAllMenus();
            GroupingContextMenuType = obj;
        }

        private sealed class PrototypeAndStatesContextMenuComparer : IEqualityComparer<IEntity>
        {
            private static readonly List<Func<IEntity, IEntity, bool>> EqualsList = new()
            {
                (a, b) => (a.Prototype.ID == b.Prototype.ID),
                (a, b) =>
                {
                    var xStates = a.GetComponent<ISpriteComponent>().AllLayers.Where(e => e.Visible).Select(s => s.RsiState.Name);
                    var yStates = b.GetComponent<ISpriteComponent>().AllLayers.Where(e => e.Visible).Select(s => s.RsiState.Name);

                    return xStates.OrderBy(t => t).SequenceEqual(yStates.OrderBy(t => t));
                },
            };
            private static readonly List<Func<IEntity, int>> GetHashCodeList = new()
            {
                e => EqualityComparer<string>.Default.GetHashCode(e.Prototype.ID),
                e =>
                {
                    var hash = 0;
                    foreach (var element in e.GetComponent<ISpriteComponent>().AllLayers.Where(obj => obj.Visible).Select(s => s.RsiState.Name))
                    {
                        hash ^= EqualityComparer<string>.Default.GetHashCode(element);
                    }
                    return hash;
                },
            };

            public static int Count => EqualsList.Count - 1;

            private readonly int _depth;

            public PrototypeAndStatesContextMenuComparer(int step = 0)
            {
                _depth = step > Count ? Count : step;
            }

            public bool Equals(IEntity x, IEntity y)
            {
                return EqualsList[_depth](x, y);
            }

            public int GetHashCode(IEntity x)
            {
                return GetHashCodeList[_depth](x);
            }
        }

        private sealed class PrototypeContextMenuComparer : IEqualityComparer<IEntity>
        {
            public bool Equals(IEntity a, IEntity b)
            {
                if (a.Prototype.ID == b.Prototype.ID)
                {
                    var xStates = a.GetComponent<ISpriteComponent>().AllLayers.Where(e => e.Visible).Select(s => s.RsiState.Name);
                    var yStates = b.GetComponent<ISpriteComponent>().AllLayers.Where(e => e.Visible).Select(s => s.RsiState.Name);

                    return xStates.OrderBy(t => t).SequenceEqual(yStates.OrderBy(t => t));
                }
                return false;
            }

            public int GetHashCode(IEntity e)
            {
                var hash = EqualityComparer<string>.Default.GetHashCode(e.Prototype.ID);
                foreach (var element in e.GetComponent<ISpriteComponent>().AllLayers.Where(obj => obj.Visible).Select(s => s.RsiState.Name))
                {
                    hash ^= EqualityComparer<string>.Default.GetHashCode(element);
                }

                return hash;
            }
        }

    }
}
