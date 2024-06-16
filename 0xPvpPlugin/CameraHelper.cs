using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OPP
{
    public  static class CameraHelper
    {
        public unsafe static bool CanSee(Vector3 myPoint, Vector3 targetPoint)
        {
            myPoint.Y += 3;
            targetPoint.Y += 3;

            var vec = targetPoint - myPoint;
            var dis = vec.Length() - 0.1f;

            int* unknown = stackalloc int[] { 0x4000, 0, 0x4000, 0 };

            RaycastHit hit = default;

            return !FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->BGCollisionModule
                ->RaycastEx(&hit, myPoint, vec, dis, 1, unknown);
        }
    }
}

