﻿using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.Server.Inventory;

[RegisterComponent]
[ComponentReference(typeof(InventoryComponent))]
public sealed class ServerInventoryComponent : InventoryComponent { }
