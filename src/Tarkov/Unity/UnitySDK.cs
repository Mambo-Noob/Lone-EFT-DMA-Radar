/*
 * Lone EFT DMA Radar
 * Brought to you by Lone (Lone DMA)
 * 
MIT License
Copyright (c) 2025 Lone DMA
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 *
*/
using LoneEftDmaRadar.Tarkov.Unity.Structures;

namespace LoneEftDmaRadar.Tarkov.Unity
{
    public readonly struct UnitySDK
    {
        public readonly struct UnityOffsets
        {
            // Module Base
            public const uint GameObjectManager = 0x1A1F0B8;
            public const uint AllCameras = 0x19EE040; // Lookup in IDA 's_AllCamera'
            public const uint PhysiX = 0x1ACD770; // g_PhysiX
            public const uint GameObject_ObjectClassOffset = 0x80;
            public const uint GameObject_ComponentsOffset = 0x50;
            public const uint GameObject_NameOffset = 0x80;
            public const uint MonoBehaviour_ObjectClassOffset = 0x38;
            public const uint MonoBehaviour_GameObjectOffset = 0x48;
            public const uint MonoBehaviour_EnabledOffset = 0x38;
            public const uint MonoBehaviour_IsAddedOffset = 0x39;
            public const uint Component_ObjectClassOffset = 0x38;
            public const uint Component_GameObjectOffset = 0x50;          
            public const uint TransformInternal_TransformAccessOffset = 0x90; // to TransformAccess
            public const uint TransformAccess_IndexOffset = 0x70;
            public const uint TransformAccess_HierarchyOffset = 0x68;
            public const uint Hierarchy_VerticesOffset = 0x38;
            public const uint Hierarchy_IndicesOffset = 0x40;
            public const uint Hierarchy_RootPositionOffset = 0x40;
            public const uint Camera_ViewMatrixOffset = 0x120; // m_WorldToCameraMatrix Matrix4x4
            public const uint Camera_FOVOffset = 0x1A0;
            public const uint Camera_AspectRatioOffset = 0x510;
            public const uint Camera_ZoomLevelOffset = 0xE0;
            
            // GfxDeviceClient
            //public const uint GfxDeviceClient_ViewportOffset = 0x25A0; // m_Viewport RectT<int>
            
            // UnityInputManager
            //public const uint UnityInputManager_CurrentKeyStateOffset = 0x60; // 0x50 + 0x8
            
            // Chains
            public static readonly uint[] GameWorldChain =
            [
                GameObject_ComponentsOffset,
                0x18,
                Component_ObjectClassOffset
            ];

            public static readonly uint[] TransformChain =
            [
                ObjectClass.MonoBehaviourOffset,
                Component_GameObjectOffset,
                GameObject_ComponentsOffset,
                0x8,
                Component_ObjectClassOffset,
                0x10 // Transform Internal
            ];
        }
    }
}