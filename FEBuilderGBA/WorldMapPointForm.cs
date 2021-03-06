﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;

using System.Text;
using System.Windows.Forms;

namespace FEBuilderGBA
{
    public partial class WorldMapPointForm : Form
    {
        public WorldMapPointForm()
        {
            InitializeComponent();

            this.InputFormRef = Init(this);
            this.InputFormRef.MakeGeneralAddressListContextMenu(true);
        }

        public InputFormRef InputFormRef;
        static InputFormRef Init(Form self)
        {
            return new InputFormRef(self
                , ""
                , Program.ROM.RomInfo.worldmap_point_pointer()
                , 32
                , (int i, uint addr) =>
                {//12 16 20 の店が、ポインタ or nullであれば
                    return U.isPointerOrNULL(Program.ROM.u32(addr + 12))
                        && U.isPointerOrNULL(Program.ROM.u32(addr + 16))
                        && U.isPointerOrNULL(Program.ROM.u32(addr + 20))
                        ;
                }
                , (int i, uint addr) =>
                {
                    uint id = Program.ROM.u16(addr + 28);
                    return U.ToHexString(i) + " " + TextForm.Direct(id);
                }
                );
        }

        private void WorldMapPointForm_Load(object sender, EventArgs e)
        {
            MapPictureBox.HideCommandBar();
            MapPictureBox.LoadWorldMap();
            MapPictureBox.SetChipSize(1);

            for (uint i = 0; i < WorldMapPathForm.DataCount(); i++)
            {
                List<MapPictureBox.StaticItem> list = WorldMapPathForm.DrawPath(i);
                for (int n = 0; n < list.Count; n++)
                {
                    MapPictureBox.SetStaticItem("road" + i.ToString() + "_" + n.ToString(), list[n].x, list[n].y, list[n].bitmap);
                }
                MapPictureBox.Invalidate();
            }
        }

        public static List<U.AddrResult> MakeWorldMapPointList()
        {
            InputFormRef InputFormRef = Init(null);
            return InputFormRef.MakeList();
        }
        public static List<U.AddrResult> MakeWorldMapPointList(Func<uint, bool> condCallback)
        {
            InputFormRef InputFormRef = Init(null);
            return InputFormRef.MakeList(condCallback);
        }

        public static List<U.AddrResult> GetShopAddr(uint addr)
        {
            List<U.AddrResult> ret = new List<U.AddrResult>();
            uint textid = Program.ROM.u16(addr + 28);

            uint shopaddr;
            shopaddr = Program.ROM.p32(addr + 12);
            if (U.isSafetyOffset( shopaddr ) )
            {
                string name = TextForm.Direct(textid) + " " + R._("武器屋");
                ret.Add(new U.AddrResult(shopaddr,name , addr + 12));
            }

            shopaddr = Program.ROM.p32(addr + 16);
            if (U.isSafetyOffset( shopaddr ) )
            {
                string name = TextForm.Direct(textid) + " " + R._("道具屋");
                ret.Add(new U.AddrResult(shopaddr,name , addr + 16));
            }   

            shopaddr = Program.ROM.p32(addr + 20);
            if (U.isSafetyOffset( shopaddr ) )
            {
                string name = TextForm.Direct(textid) + " " + R._("秘密の店");
                ret.Add(new U.AddrResult(shopaddr,name , addr + 20));
            }

            return ret;
        }
        public static string GetWorldMapPointName(uint baseid)
        {
            InputFormRef InputFormRef = Init(null);
            uint addr = InputFormRef.IDToAddr(baseid);
            if (!U.isSafetyOffset(addr))
            {
                return "";
            }
            uint textid = Program.ROM.u16(addr+28);
            return TextForm.Direct(textid);
        }
        public static Point GetWorldMapPointPosstion(uint baseid,bool isCenter)
        {
            InputFormRef InputFormRef = Init(null);
            uint addr = InputFormRef.IDToAddr(baseid);
            if (!U.isSafetyOffset(addr))
            {
                return new Point();
            }

            uint x = Program.ROM.u16(addr + 24);
            uint y = Program.ROM.u16(addr + 26);

            if (isCenter)
            {
                uint icon = Program.ROM.u8(addr + 2);
                Bitmap basepoint = WorldMapImageForm.DrawWorldMapIcon(icon);
                x = (uint)(x + basepoint.Width / 2);
                y = (uint)(y + basepoint.Height / 2);
            }
            return new Point((int)x,(int)y);    
        }

        private void AddressList_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        public static void DrawBasePointAddr(Bitmap map,uint addr)
        {
            uint icon = Program.ROM.u8(addr+2);
            uint x = Program.ROM.u16(addr+24);
            uint y = Program.ROM.u16(addr+26);
            Bitmap basepoint = WorldMapImageForm.DrawWorldMapIcon(icon);
            int paletteMapping = ImageUtil.FindPalette(map, basepoint);
            if (paletteMapping <= -1)
            {
                paletteMapping = ImageUtil.AppendPalette(map, basepoint);
            }
            ImageUtil.BitBlt(map
                , (int)x - (basepoint.Width / 2), (int)y - (basepoint.Height/2)
                , basepoint.Width, basepoint.Height
                , basepoint, 0, 0
                , paletteMapping,0);
        }

        public static MapPictureBox.StaticItem DrawBasePointAddr(uint addr)
        {
            MapPictureBox.StaticItem item = new MapPictureBox.StaticItem();

            uint icon = Program.ROM.u8(addr + 2);
            item.x = (int)Program.ROM.u16(addr + 24);
            item.y = (int)Program.ROM.u16(addr + 26);
            item.bitmap = WorldMapImageForm.DrawWorldMapIcon(icon);
            item.draw_x_add -= item.bitmap.Width / 2;
            item.draw_y_add -= item.bitmap.Height / 2;

            return item;
        }
        //全データの取得
        public static void MakeAllDataLength(List<Address> list)
        {
            string name = "WorldMapPoint";
            InputFormRef InputFormRef = Init(null);
            FEBuilderGBA.Address.AddAddress(list, InputFormRef, name, new uint[] { 12, 16 , 20});
        }

        private void MapPictureBox_Load(object sender, EventArgs e)
        {

        }

        public static void MakeTextIDArray(List<TextID> list)
        {
            InputFormRef InputFormRef = Init(null);
            TextID.AppendTextID(list, FELint.Type.WMAP_BASE_POINT, InputFormRef, new uint[] { 28 });
        }
    }
}
