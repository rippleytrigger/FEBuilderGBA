﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

namespace FEBuilderGBA
{
    public class MagicSplitUtil
    {
        //魔法分離パッチの判別.
        public enum magic_split_enum
        {
             NO             //なし
           , FE8NMAGIC      //for FE8J   FE8N 魔力分離
           , FE7UMAGIC      //for FE7U   魔力分離
           , NoCache = 0xFF
        };
        static magic_split_enum g_Cache_magic_split_enum = magic_split_enum.NoCache;
        static uint UnitTag;
        static uint ClassTag;
        public static magic_split_enum SearchMagicSplit()
        {
            if (g_Cache_magic_split_enum == magic_split_enum.NoCache)
            {
                g_Cache_magic_split_enum = SearchMagicSplitLow();

                UnitTag = U.NOT_FOUND;
                ClassTag = U.NOT_FOUND;
                if (g_Cache_magic_split_enum == magic_split_enum.FE7UMAGIC)
                {
                    FE7UInit();
                }
            }
            return g_Cache_magic_split_enum;
        }
        public static void ClearCache()
        {
            g_Cache_magic_split_enum = magic_split_enum.NoCache;
        }

        static magic_split_enum SearchMagicSplitLow()
        {
            string filename = U.ConfigDataFilename("magic_split_extends_");
            if (!U.IsRequiredFileExist(filename))
            {
                return magic_split_enum.NO;
            }

            string[] lines = File.ReadAllLines(filename);
            string version = Program.ROM.VersionToFilename();
            for (int i = 0; i < lines.Length; i++)
            {
                if (U.IsComment(lines[i]))
                {
                    continue;
                }
                string line = U.ClipComment(lines[i]);
                string[] sp = line.Split('\t');
                if (sp.Length < 3)
                {
                    continue;
                }
                if (sp[1] != version)
                {
                    continue;
                }

                string[] hexStrings = sp[3].Split(' ');
                byte[] need = new byte[hexStrings.Length];
                for (int n = 0; n < hexStrings.Length; n++)
                {
                    need[n] = (byte)U.atoh(hexStrings[n]);
                }

                //チェック開始アドレス
                uint start = U.atoh(sp[2]);

                byte[] data = Program.ROM.getBinaryData(start, need.Length);
                if (U.memcmp(need, data) != 0)
                {
                    continue;
                }
                if (sp[0] == "FE8NMAGIC")
                {

                    return magic_split_enum.FE8NMAGIC;
                }
                if (sp[0] == "FE7UMAGIC")
                {
                    return magic_split_enum.FE7UMAGIC;
                }
                
            }
            return magic_split_enum.NO;
        }
        
        static void FE7UInit()
        {
            string filename ;
            uint p;
            filename = Path.Combine(Program.BaseDirectory,"config", "patch2", "FE7U", "FE7-Str Mag Split", "Autolevelling and Saves", "Char Mag Autolevel.dmp");
            p = U.Grep4EndByDmp(filename, 0x100, 0);
            if (U.isSafetyOffset(p))
            {
                UnitTag = Program.ROM.p32(p);
            }

            filename = Path.Combine(Program.BaseDirectory, "config", "patch2", "FE7U", "FE7-Str Mag Split", "Autolevelling and Saves", "Class Mag Autolevel.dmp");
            p = U.Grep4EndByDmp(filename, 0x100, 0);
            if (U.isSafetyOffset(p))
            {
                ClassTag = Program.ROM.p32(p);
            }
        }

        static void WriteUnitAs(uint uid, uint skip,uint newValue,Undo.UndoData undodata)
        {
            if (!U.isSafetyOffset(UnitTag))
            {
                return ;
            }
            uint a = UnitTag + (uid * 2) + skip;
            if (!U.isSafetyOffset(a))
            {
                return ;
            }
            Program.ROM.write_u8(a, newValue , undodata);
        }
        static void WriteClassAs(uint cid, uint skip, uint newValue, Undo.UndoData undodata)
        {
            if (!U.isSafetyOffset(ClassTag))
            {
                return ;
            }
            uint a = ClassTag + (cid * 4) + skip;
            if (!U.isSafetyOffset(a))
            {
                return ;
            }
            Program.ROM.write_u8(a, newValue, undodata);
        }

        static uint GetUnitAs(uint uid, uint skip)
        {
            if (!U.isSafetyOffset(UnitTag))
            {
                return 0;
            }
            uint a = UnitTag + (uid * 2) + skip;
            if (!U.isSafetyOffset(a))
            {
                return 0;
            }
            return Program.ROM.u8(a);
        }
        static uint GetClassAs(uint cid, uint skip)
        {
            if (!U.isSafetyOffset(ClassTag))
            {
                return 0;
            }
            uint a = ClassTag + (cid * 4) + skip;
            if (!U.isSafetyOffset(a))
            {
                return 0;
            }
            return Program.ROM.u8(a);
        }

        static public uint GetUnitBaseMagicExtends(uint uid, uint addr)
        {
            if (g_Cache_magic_split_enum == magic_split_enum.FE7UMAGIC)
            {
                return GetUnitAs(uid, 0);
            }
            else if (g_Cache_magic_split_enum == magic_split_enum.FE8NMAGIC)
            {
                return Program.ROM.u8(addr + 50);
            }
            return 0;
        }
        static public uint GetUnitGrowMagicExtends(uint uid, uint addr)
        {
            if (g_Cache_magic_split_enum == magic_split_enum.FE7UMAGIC)
            {
                return GetUnitAs(uid, 1);
            }
            else if (g_Cache_magic_split_enum == magic_split_enum.FE8NMAGIC)
            {
                return Program.ROM.u8(addr + 51);
            }
            return 0;
        }



        static public uint GetClassBaseMagicExtends(uint cid, uint addr)
        {
            if (g_Cache_magic_split_enum == magic_split_enum.FE7UMAGIC)
            {
                return GetClassAs(cid, 0);
            }
            else if (g_Cache_magic_split_enum == magic_split_enum.FE8NMAGIC)
            {
                return Program.ROM.u8(addr + 80);
            }
            return 0;
        }

        static public uint GetClassGrowMagicExtends(uint cid, uint addr)
        {
            if (g_Cache_magic_split_enum == magic_split_enum.FE7UMAGIC)
            {
                return GetClassAs(cid, 1);
            }
            else if (g_Cache_magic_split_enum == magic_split_enum.FE8NMAGIC)
            {
                return Program.ROM.u8(addr + 81);
            }
            return 0;
        }
        static public uint GetClassLimitMagicExtends(uint cid, uint addr)
        {
            if (g_Cache_magic_split_enum == magic_split_enum.FE7UMAGIC)
            {
                return GetClassAs(cid, 2);
            }
            else if (g_Cache_magic_split_enum == magic_split_enum.FE8NMAGIC)
            {//体力を使いまわす
                return Program.ROM.u8(addr + 25);
            }
            return 0;
        }
        static public uint GetClassPromotionGainMagicExtends(uint cid, uint addr)
        {
            if (g_Cache_magic_split_enum == magic_split_enum.FE7UMAGIC)
            {
                return GetClassAs(cid, 3);
            }
            else if (g_Cache_magic_split_enum == magic_split_enum.FE8NMAGIC)
            {
                return Program.ROM.u8(addr + 82);
            }
            return 0;
        }


        static public void WriteUnitBaseMagicExtends(uint uid, uint addr, uint newValue, Undo.UndoData undodata)
        {
            if (g_Cache_magic_split_enum == magic_split_enum.FE7UMAGIC)
            {
                WriteUnitAs(uid, 0, newValue, undodata);
                return;
            }
//            else if (g_Cache_magic_split_enum == magic_split_enum.FE8NMAGIC)
//            {//Unit構造体の未使用領域を利用するため不要
//            }
        }
        static public void WriteUnitGrowMagicExtends(uint uid, uint addr, uint newValue, Undo.UndoData undodata)
        {
            if (g_Cache_magic_split_enum == magic_split_enum.FE7UMAGIC)
            {
                WriteUnitAs(uid, 1 , newValue , undodata);
                return;
            }
//            else if (g_Cache_magic_split_enum == magic_split_enum.FE8NMAGIC)
//            {//Unit構造体の未使用領域を利用するため不要
//            }
        }

        static public void WriteClassBaseMagicExtends(uint cid, uint addr, uint newValue, Undo.UndoData undodata)
        {
            if (g_Cache_magic_split_enum == magic_split_enum.FE7UMAGIC)
            {
                WriteClassAs(cid, 0 , newValue , undodata);
                return;
            }
//            else if (g_Cache_magic_split_enum == magic_split_enum.FE8NMAGIC)
//            {//Class構造体の未使用領域を利用するため不要
//            }
        }

        static public void WriteClassGrowMagicExtends(uint cid, uint addr, uint newValue, Undo.UndoData undodata)
        {
            if (g_Cache_magic_split_enum == magic_split_enum.FE7UMAGIC)
            {
                WriteClassAs(cid, 1, newValue, undodata);
                return;
            }
//            else if (g_Cache_magic_split_enum == magic_split_enum.FE8NMAGIC)
//            {//Class構造体の未使用領域を利用するため不要
//            }
        }
        static public void WriteClassLimitMagicExtends(uint cid, uint addr, uint newValue, Undo.UndoData undodata)
        {
            if (g_Cache_magic_split_enum == magic_split_enum.FE7UMAGIC)
            {
                WriteClassAs(cid, 2, newValue, undodata);
                return;
            }
//            else if (g_Cache_magic_split_enum == magic_split_enum.FE8NMAGIC)
//            {//体力を使いまわす
//                return Program.ROM.u8(addr + 25);
//            }
        }
        static public void WriteClassPromotionGainMagicExtends(uint cid, uint addr, uint newValue, Undo.UndoData undodata)
        {
            if (g_Cache_magic_split_enum == magic_split_enum.FE7UMAGIC)
            {
                WriteClassAs(cid, 3, newValue, undodata);
                return;
            }
//            else if (g_Cache_magic_split_enum == magic_split_enum.FE8NMAGIC)
//            {//Class構造体の未使用領域を利用するため不要
//            }
        }

    }
}
