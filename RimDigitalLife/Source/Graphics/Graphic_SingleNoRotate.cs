using RimWorld;
using UnityEngine;
using Verse;

namespace RimDigitalLife
{
    public class Graphic_SingleNoRotate : Graphic_Single
    {
        public override bool ShouldDrawRotated => false;

        public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
        {
            return GraphicDatabase.Get<Graphic_SingleNoRotate>(path, newShader, drawSize, newColor, newColorTwo, data);
        }
    }
}
