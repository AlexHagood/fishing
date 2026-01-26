# Terrain Tools Plugin

An integrated editor plugin for sculpting terrain in Godot 4.5+.

## Features

- **Integrated brush tool**: No need to add brush nodes to your scene
- **Visual toolbar**: Easy-to-use controls in the 3D editor
- **Real-time preview**: See exactly where you're painting
- **Adjustable parameters**: Control radius and strength on the fly

## How to Use

### 1. Enable the Plugin

1. Go to **Project → Project Settings → Plugins**
2. Enable "Terrain Tools"

### 2. Edit Terrain

1. Select a **Terrain** node in the scene tree
2. The Terrain Tools toolbar will appear at the top of the 3D viewport
3. Click **"Enable Terrain Brush"** button to activate the brush

### 3. Sculpt the Terrain

- **Raise terrain**: `Left Mouse Button` (LMB)
- **Lower terrain**: `Right Mouse Button` (RMB)
- **Crack panel**: `Shift` + `Left Mouse Button` - Splits a triangle into 3 smaller triangles by adding a node
- **Camera pan**: `Middle Mouse Button` (MMB) - Still works while brush is active
- **Camera zoom**: `Mouse Wheel` - Still works while brush is active
- **Adjust radius**: Use the Radius slider (0.5 - 10.0)
- **Adjust strength**: Use the Strength slider (0.1 - 5.0)

### 4. Disable Brush

- Click **"Enable Terrain Brush"** button again to deactivate
- Or simply select a different node

## Technical Details

The plugin:
- Automatically manages brush preview (no scene clutter)
- Only activates when a Terrain node is selected
- Uses internal nodes that don't save to your scene
- Works in editor mode only

## Notes

- The brush requires `Shift` to be held to prevent accidental edits
- The green sphere shows where the brush will affect
- Red = lowering, Green = raising
- Terrain modifications are immediate (undo/redo coming in future version)
