﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Text;

namespace FEBuilderGBA
{
    //EAの簡易パーサ 今は、簡単なことしかできません。
    class EAUtil
    {
        public enum DataEnum
        {
            ORG
            ,MIX //incbinされたデータ 判別不能
            ,ASM //incbinされたデータ ASM
            ,BIN //incbinされたデータ BIN
            ,LYN //lynによってインポートされるelfファイル
            ,POINTER_ARRAY
        }
        public class Data
        {
            public string Name { get; private set; }
            public uint ORGAddr { get; private set; }
            public byte[] BINData { get; private set; }
            public DataEnum DataType { get; private set; }

            public Data(uint orgaddr, DataEnum dataType)
            {
                this.ORGAddr = orgaddr;
                this.DataType = dataType;
            }
            public Data(string name, byte[] data, DataEnum dataType)
            {
                this.Name = name;
                this.BINData = data;
                this.DataType = dataType;
            }
        }
        public List<Data> DataList { get; private set; }
        public List<string> IfNDefList { get; private set; }
        public string Filename { get; private set; }
        public string Dir { get; private set; }

        public EAUtil(string filename)
        {
            Parse(filename);
        }

        void Parse(string filename)
        {
            this.DataList = new List<Data>();
            this.Filename = filename;
            this.Dir = Path.GetDirectoryName(filename);
            this.IfNDefList = new List<string>();

            this.CurrentLabel = "";
            string[] lines = File.ReadAllLines(filename);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = U.ClipComment(lines[i], isWithoutSharpComment: true);
                if (line == "")
                {
                    continue;
                }

                ParseJumpToHack(line);
                ParseIfNDef(line);
            }
            //本格的なパース.
            for (int i = 0; i < lines.Length; i++)
            {
                string line = U.ClipComment(lines[i], isWithoutSharpComment: true);
                if (line == "")
                {
                    continue;
                }
                ParseORG(line);
                ParseIncBIN(line, lines[i]);
                ParseLynELF(line, lines[i]);
                ParseLynHook(line, lines[i]);
                ParsePng2Dmp(line, lines[i]);
                ParseLabel(line , lines[i]);
            }
        }
        void ParseLabel(string line, string orignalIine)
        {
            string a = line.Trim();
            int pos = a.IndexOf(':');
            if (pos < 0)
            {
                return ;
            }
            this.CurrentLabel = a.Substring(0, pos);


            if (orignalIine.IndexOf("HINT=POINTER_ARRAY", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Data data = new Data(this.CurrentLabel, new byte[] { }, DataEnum.POINTER_ARRAY);
                this.DataList.Add(data);
            }

        }
        int FindJumpToHack(string line)
        {
            int pos = line.IndexOf("jumpToHack", StringComparison.OrdinalIgnoreCase);
            if (pos >= 0)
            {
                return pos;
            }
            pos = line.IndexOf("callHack", StringComparison.OrdinalIgnoreCase);
            if (pos >= 0)
            {
                return pos;
            }
            pos = line.IndexOf("replaceWithHack", StringComparison.OrdinalIgnoreCase);
            if (pos >= 0)
            {
                return pos;
            }
            return -1;
        }
        void ParseIfNDef(string line)
        {
            int pos = line.IndexOf("#ifndef", StringComparison.OrdinalIgnoreCase);
            if (pos < 0)
            {
                return;
            }

            string ifdef_keyword = line.Substring(pos + 7 + 1).Trim();
            if (ifdef_keyword == "")
            {
                return;
            }
            IfNDefList.Add(ifdef_keyword);
        }
        void ParseJumpToHack(string line)
        {
            int pos = FindJumpToHack(line);
            if (pos < 0)
            {
                return;
            }
            string label = U.cut(line, "(", ")");
            JumpToHackLabeles.Add(label);
        }
        bool isCode(string fullPath)
        {
            //ソースコードがあればASMだろう.
            string srcFilename = U.ChangeExtFilename(fullPath, ".s");
            if (File.Exists(srcFilename))
            {//ソースコードがあったのでASMです
                return true;
            }
            srcFilename = U.ChangeExtFilename(fullPath, ".asm");
            if (File.Exists(srcFilename))
            {//ソースコードがあったのでASMです
                return true;
            }

            if (this.JumpToHackLabeles.IndexOf(this.CurrentLabel) >= 0)
            {//JumpToHackで呼び出されているのでASMです
                return true;
            }
            return false;
        }

        string CurrentLabel;
        List<string> JumpToHackLabeles = new List<string>();


        bool ParseIncBIN(string line , string orignalIine)
        {
            string a = Keyword(line, "#incbin");
            if (a == "")
            {
                return false;
            }
            string filename = U.cut(a, "\"", "\"");
            string fullbinname = Path.Combine( this.Dir, filename);

            if (!File.Exists(fullbinname))
            {
                return false;
            }

            DataEnum dataType ;
            if (orignalIine.IndexOf("HINT=BIN", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                dataType = DataEnum.BIN;
            }
            else if (orignalIine.IndexOf("HINT=ASM", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                dataType = DataEnum.ASM;
            }
            else
            {//ヒントがない場合は、 JumpToHackの有無で判断します.
                if (isCode(fullbinname))
                {
                    dataType = DataEnum.ASM;
                }
                else
                {
                    dataType = DataEnum.MIX;
                }
            }

            Data data = new Data(filename, File.ReadAllBytes(fullbinname), dataType);
            this.DataList.Add(data);
            return true;
        }
        bool ParseLynHook(string line, string orignalIine)
        {
            string keyword = "HINT=LYN_HOOK=";
            int pos = orignalIine.IndexOf(keyword);
            if (pos < 0)
            {
                return false;
            }
            uint orgaddr = U.atoi0x(orignalIine.Substring(pos + keyword.Length));

            Data data = new Data(orgaddr, DataEnum.ORG);
            this.DataList.Add(data);
            return true;
        }

        bool ParseLynELF(string line , string orignalIine)
        {
            string a = Keyword(line, "#inctevent lyn");
            if (a == "")
            {
                a = Keyword(line, "#inctext lyn");
                if (a == "")
                {
                    return false;
                }
            }
            string filename = U.cut(a, "\"", "\"");
            string fullbinname = Path.Combine( this.Dir, filename);

            if (!File.Exists(fullbinname))
            {
                return false;
            }

            DataEnum dataType ;
            dataType = DataEnum.LYN;

            Elf elf = new Elf(fullbinname);
            Data data = new Data(filename, elf.ProgramBIN, dataType);
            this.DataList.Add(data);
            return true;
        }

        bool ParsePng2Dmp(string line, string orignalIine)
        {
            string a = Keyword(line, "#incext Png2Dmp");
            if (a == "")
            {
                return false;
            }
            string filename = U.cut(a, "\"", "\"");
            string fullbinname = Path.Combine(this.Dir, filename);

            if (!File.Exists(fullbinname))
            {
                return false;
            }

            DataEnum dataType = DataEnum.BIN;
            if (orignalIine.IndexOf("--lz77") >= 0)
            {
                byte[] image = Png2DmpLZ77(fullbinname);
                Data data = new Data(filename, image, dataType);
                this.DataList.Add(data);
            }

            string errorMessage;
            Bitmap bitmap = ImageUtil.OpenBitmap(fullbinname, null, out errorMessage);
            if (bitmap == null)
            {
                return false;
            }
            if (bitmap.Width < 8 || bitmap.Height < 8)
            {
                bitmap.Dispose();
                return false;
            }

            if (orignalIine.IndexOf("--palette-only") >= 0)
            {//パレットのみ
                byte[] palette = ImageUtil.ImageToPalette(bitmap, 1);

                Data data = new Data(filename, palette, dataType);
                this.DataList.Add(data);
            }
            else
            {
                byte[] image = ImageUtil.ImageToByte16Tile(bitmap, bitmap.Width, bitmap.Height);

                Data data = new Data(filename, image, dataType);
                this.DataList.Add(data);
            }

            bitmap.Dispose();

            return true;
        }
        byte[] Png2DmpLZ77(string filename)
        {
            string hint = filename + ".dmp";
            if (File.Exists(hint))
            {
                return File.ReadAllBytes(hint);
            }
            //ヒントファイルがない!!
            Debug.Assert(false);
            return new byte[] { };
        }

        bool ParseORG(string line)
        {
            string a = Keyword(line , "ORG");
            if (a == "")
            {
                return false;
            }
            uint addr = U.atoi0x(a);
            if (addr <= 0)
            {
                return false;
            }
            addr = U.toOffset(addr);
            if (!U.isSafetyOffset(addr))
            {
                return false;
            }

            Data data = new Data(addr, DataEnum.ORG);
            this.DataList.Add(data);
            return true;
        }

        string Keyword(string line,string keyword)
        {
            int pos = line.IndexOf(keyword + " ", StringComparison.OrdinalIgnoreCase);
            if (pos < 0)
            {
                pos = line.IndexOf(keyword + "\t", StringComparison.OrdinalIgnoreCase);
                if (pos < 0)
                {
                    return "";
                }
            }
            string r = line.Substring(pos + keyword.Length + 1).Trim();
            return r;
        }

        public static bool IsFBGTemp(string filename)
        {
            string file = Path.GetFileName(filename);
            return (file.IndexOf("_FBG_Temp_") == 0);
        }

    }
}
