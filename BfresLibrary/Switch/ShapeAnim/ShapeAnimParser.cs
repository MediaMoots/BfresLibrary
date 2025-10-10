using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BfresLibrary.Switch.Core;
using BfresLibrary.Core;
using BfresLibrary;

namespace BfresLibrary.Switch
{
    public class ShapeAnimParser
    {
        internal static void Read(ResFileSwitchLoader loader, ShapeAnim shapeAnim)
        {
            if (loader.ResFile.VersionMajor2 < 9)
            {
                throw new Exception();
            }

            shapeAnim.Flags = loader.ReadEnum<ShapeAnim.ShapeAnimFlags>(true);
            loader.ReadUInt16();

            shapeAnim.Name = loader.LoadString();
            shapeAnim.Path = loader.LoadString();
            shapeAnim.BindModel = loader.Load<Model>();
            uint BindIndicesOffset = loader.ReadOffset();
            uint VertexShapeAnimsArrayOffset = loader.ReadOffset();
            shapeAnim.UserData = loader.LoadDictValues<UserData>();

            shapeAnim.FrameCount = loader.ReadInt32();
            shapeAnim.BakedSize = loader.ReadUInt32();
            ushort numUserData = loader.ReadUInt16();
            ushort numVertexShapeAnim = loader.ReadUInt16();
            ushort numKeyShapeAnim = loader.ReadUInt16();
            ushort numCurve = loader.ReadUInt16();

            shapeAnim.BindIndices = loader.LoadCustom(() => loader.ReadUInt16s(numVertexShapeAnim), BindIndicesOffset);
            shapeAnim.VertexShapeAnims = loader.LoadList<VertexShapeAnim>(numVertexShapeAnim, VertexShapeAnimsArrayOffset);
        }

        public static void Write(ResFileSwitchSaver saver, ShapeAnim shapeAnim)
        {
            if (saver.ResFile.VersionMajor2 < 9)
            {
                throw new Exception();
            }

            saver.Write(shapeAnim.Flags, true);
            saver.Write((ushort)0);

            saver.SaveString(shapeAnim.Name);
            saver.SaveString(shapeAnim.Path);
            shapeAnim.PosBindModelOffset = saver.SaveOffset();
            shapeAnim.PosBindIndicesOffset = saver.SaveOffset();
            shapeAnim.PosVertexShapeAnimsOffset = saver.SaveOffset();
            shapeAnim.PosUserDataOffset = saver.SaveOffset();
            shapeAnim.PosUserDataDictOffset = saver.SaveOffset();

            saver.Write(shapeAnim.FrameCount);
            saver.Write(shapeAnim.BakedSize);
            saver.Write((ushort)shapeAnim.UserData.Count);
            saver.Write((ushort)shapeAnim.VertexShapeAnims.Count);
            saver.Write((ushort)shapeAnim.VertexShapeAnims.Sum((x) => x.KeyShapeAnimInfos.Count));
            saver.Write((ushort)shapeAnim.VertexShapeAnims.Sum((x) => x.Curves.Count));
        }
    }
}
