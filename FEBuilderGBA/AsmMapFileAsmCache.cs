﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using System.IO;
using System.Text;

namespace FEBuilderGBA
{
    class AsmMapFileAsmCache
    {
        AsmMapFile CachedMAP = null;
  
        //即席で結果を返せるものだけで作成する.
        AsmMapFile MakeInstant()
        {
            AsmMapFile map = new AsmMapFile();

            {
                List<Address> list = new List<Address>();
                uint maxcount = MapSettingForm.GetDataCount();
                for (uint mapid = 0; mapid < maxcount; mapid++)
                {
                    //イベント命令　一覧の取得(開始イベントと終了イベントのみ)
                    List<U.AddrResult> eventlist = EventCondForm.MakeEventScriptPointerStartAndEndEventOnly(mapid);
                    for (int i = 0; i < eventlist.Count; i++)
                    {
                        if (!U.isSafetyOffset(eventlist[i].addr))
                        {
                            continue;
                        }

                        Address.AddAddress(list
                            , eventlist[i].addr
                            , 4
                            , U.NOT_FOUND
                            , eventlist[i].name 
                            , Address.DataTypeEnum.EVENTSCRIPT);
                    }
                }
                map.AppendMAP(list, "EVENT");
            }
            //イベント命令にあるASM命令
            {
                List<Address> list = new List<Address>();
                EventScript.MakeEventASMMAPList(list, false, "", true);
                map.AppendMAP(list);
            }
            //イベント命令にあるEVENT命令
            {
                List<Address> list = new List<Address>();
                EventScript.MakeEventASMMAPList(list, true, "", true);
                map.AppendMAP(list, "EVENT");
            }
            map.MakeNearSearchSortedList();

            return map;
        }
        //全部のデータを作る 時間がかかる.
        AsmMapFile MakeFull()
        {
#if !DEBUG 
            try
            {
#endif
                return MakeFullLow();
#if !DEBUG 
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                return MakeInstant();
            }
#endif
        }

        AsmMapFile MakeFullLow()
        {
            AsmMapFile map = MakeInstant();
            if (IsStopFlag) return map;

            List<DisassemblerTrumb.LDRPointer> ldrmap;
#if !DEBUG 
            try
            {
#endif
            ldrmap = DisassemblerTrumb.MakeLDRMap(Program.ROM.Data, 0x100);
            if (IsStopFlag) return map;
#if !DEBUG 
            }
            catch (Exception e)
            {
                Log.Error(e.ToString() );
                return map;
            }
#endif

            List<Address> structlist;
#if !DEBUG 
            try
            {
#endif
            structlist = U.MakeAllStructPointersList(false); //既存の構造体
            if (IsStopFlag) return map;
#if !DEBUG 
            }
            catch (Exception e)
            {
                Log.Error(e.ToString() );
                return map;
            }
#endif

#if !DEBUG 
            try
            {
#endif
            PatchForm.MakePatchStructDataList(structlist,true,true,false,true); //パッチが知っている領域.
            if (IsStopFlag) return map;
#if !DEBUG 
            }
            catch (Exception e)
            {
                Log.Error(e.ToString() );
            }
#endif
#if !DEBUG 
            try
            {
#endif
            map.MakeTextIDArray(); //全テキストIDの検出
            if (IsStopFlag) return map;
#if !DEBUG 
            }
            catch (Exception e)
            {
                Log.Error(e.ToString() );
            }
#endif
#if !DEBUG 
            try
            {
#endif
            GraphicsToolForm.MakeLZ77DataList(structlist); //lz77で圧縮されたもの(主に画像)
            if (IsStopFlag) return map;
#if !DEBUG 
            }
            catch (Exception e)
            {
                Log.Error(e.ToString() );
            }
#endif
#if !DEBUG 
            try
            {
#endif
            ProcsScriptForm.MakeAllDataLength(structlist, ldrmap);
            if (IsStopFlag) return map;
#if !DEBUG 
            }
            catch (Exception e)
            {
                Log.Error(e.ToString() );
            }
#endif


#if !DEBUG 
            try
            {
#endif
            OAMSPForm.MakeAllDataLength(structlist, structlist, ldrmap);
            if (IsStopFlag) return map;
#if !DEBUG 
            }
            catch (Exception e)
            {
                Log.Error(e.ToString() );
            }
#endif
#if !DEBUG 
            try
            {
#endif
            AsmMapFile.MakeFreeDataList(structlist, 0x100, 0x00, 16); //フリー領域
            if (IsStopFlag) return map;
#if !DEBUG 
            }
            catch (Exception e)
            {
                Log.Error(e.ToString() );
            }
#endif
#if !DEBUG 
            try
            {
#endif
            AsmMapFile.MakeFreeDataList(structlist, 0x100, 0xFF, 16); //フリー領域
            if (IsStopFlag) return map;
#if !DEBUG 
            }
            catch (Exception e)
            {
                Log.Error(e.ToString() );
            }
#endif
#if !DEBUG 
            try
            {
#endif
            map.AppendMAP(structlist);
            if (IsStopFlag) return map;
#if !DEBUG 
            }
            catch (Exception e)
            {
                Log.Error(e.ToString() );
            }
#endif
            Dictionary<uint, uint> ldrtable = new Dictionary<uint, uint>();  //LDR参照データがある位置を記録します. コードの末尾などにあります. 数が多くなるのでマップする.
#if !DEBUG 
            try
            {
#endif

            AsmMapFile.MakeSwitchDataList(ldrtable, 0x100, 0);
            if (IsStopFlag) return map;
#if !DEBUG 
            }
            catch (Exception e)
            {
                Log.Error(e.ToString() );
            }
#endif
#if !DEBUG 
            try
            {
#endif

            map.AppendMAP(ldrtable);
            if (IsStopFlag) return map;
#if !DEBUG 
            }
            catch (Exception e)
            {
                Log.Error(e.ToString() );
            }
#endif

            //近場リスト生成.
            map.MakeNearSearchSortedList();

            return map;
        }


        public void ClearCache()
        {
            //キャッシュを作り直す.
            IsStopFlag = false;
            GetAsmMapFile(true);
        }

        public AsmMapFile GetAsmMapFile(bool rebuild = false)
        {
            if (CachedMAP == null)
            {//一番最初はデータがないので、
                CachedMAP = MakeInstant(); //すぐに結果を返せるものだけを作り
                BuildThread(); //時間のかかるフルマップはスレッドで生成する.
                return CachedMAP;
            }

            if (rebuild == false)
            {
                return CachedMAP;
            }
            //キャッシュの作り直し.
            BuildThread(); //時間のかかるフルマップはスレッドで生成する.
            return CachedMAP;
        }

        bool IsStopFlag = false;

        //時間のかかるフルマップはスレッドで生成する.
        delegate AsmMapFile AsyncMethodCaller();
        AsyncMethodCaller Caller = null;
        IAsyncResult AsyncResult = null;
        bool IsBusyThread()
        {
            return Caller != null && AsyncResult != null;
        }
        void BuildThread()
        {
            if (IsBusyThread())
            {//今忙しいから無理
                return;
            }
            Caller = new AsyncMethodCaller(MakeFull);
            AsyncResult = Caller.BeginInvoke(new AsyncCallback(MakeFullEndCallback), Caller);
        }
        void MakeFullEndCallback(IAsyncResult ar)
        {
            AsyncMethodCaller caller;
            AsmMapFile map;
#if !DEBUG 
            try
            {
#endif
                caller = (AsyncMethodCaller)ar.AsyncState;
                map = caller.EndInvoke(ar);
#if !DEBUG 
            }
            catch (Exception e)
            {
                Log.Error("MakeFullEndCallback");
                Log.Error(e.ToString());

                Caller = null;
                AsyncResult = null;
                IsStopFlag = false;
                return;
            }
#endif

                if (IsStopFlag)
            {
                Caller = null;
                AsyncResult = null;
                IsStopFlag = false;
                return;
            }

            CachedMAP = map;
            Caller = null;
            AsyncResult = null;
            IsStopFlag = false;
        }
        public void Join()
        {
            if (!IsBusyThread())
            {//スレッドは動作していない.
                return;
            }
            //停止命令を発動する
            IsStopFlag = true;

            try
            {
                //停止するまで待機
                while (!AsyncResult.IsCompleted)
                {
                    if (AsyncResult.AsyncWaitHandle.WaitOne(1000))
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }
           
            //停止したので、停止フラグを下す.
            IsStopFlag = false;
        }
        //停止リクエストを送る. 
        //送るだけですぐに停止するわけではない. 確実に停止したい場合はJoinを呼ぶこと.
        public void StopRequest()
        {
            if (!IsBusyThread())
            {//スレッドは動作していない.
                return;
            }
            //停止命令を発動する
            IsStopFlag = true;
        }
        public string GetName(uint num)
        {
            num = DisassemblerTrumb.ProgramAddrToPlain(num);
            string errorMessage;
            string name = GetASMName(num, true, out errorMessage);
            if (errorMessage == "")
            {
                return name;
            }
            return "";
        }
        public string GetProcsName(uint num)
        {
            string errorMessage;
            string name = GetASMName(num, true, out errorMessage);
            if (errorMessage == "")
            {
                if (name.IndexOf("Procs ") == 0)
                {
                    return name.Substring(6);
                }
            }
            return "";
        }

        public string GetASMName(uint num,bool isSWITCH, out string errorMessage)
        {
            if (num <= 0)
            {
                errorMessage = "";
                return ""; //NULL
            }

            uint plainAddr = DisassemblerTrumb.ProgramAddrToPlain(num);
            if (isSWITCH)
            {
                if (plainAddr != num)
                {//逆に危険 +1してはいけない
                    errorMessage = R._("危険なポインタです。\r\nここはswitch文のため、+1してはいけません。");
                    return R._("危険なポインタ");
                }
            }
            else
            {
                if (plainAddr == num)
                {//危険 +1 して、Thumbにするべき
                    errorMessage = R._("危険なポインタです。\r\nThumb命令で呼び出すため、ポインタに1を足した値を書く必要があります。");
                    return R._("危険なポインタ");
                }
            }
            if (!U.isSafetyPointer(num))
            {
                errorMessage = R._("無効なポインタです。\r\nこの設定は危険です。");
                return R._("無効なポインタ");
            }

            errorMessage = "";
            string comment;
            if (Program.CommentCache.TryGetValue(U.toOffset(plainAddr), out comment))
            {//ユーザー定義のコメントがある
                return comment;
            }

            AsmMapFile map = GetAsmMapFile();
            AsmMapFile.AsmMapSt a;
            if (!map.TryGetValue(U.toPointer(plainAddr), out a))
            {
                return "";
            }
            return a.Name;
        }

        public uint SearchName(string name)
        {
            AsmMapFile map = GetAsmMapFile();
            return map.SearchName(name);
        }

        public string GetEventName(uint num, out string errorMessage)
        {
            if (num <= 0)
            {
                errorMessage = "";
                return ""; //NULL
            }
            if (num == 1)
            {
                errorMessage = "";
                return R._("ダミーイベント");
            }
            if (! U.isSafetyPointer(num))
            {
                errorMessage = R._("無効なポインタです。\r\nこの設定は危険です。");
                return R._("無効なポインタ");
            }

            errorMessage = "";
            uint plainAddr = U.Padding4(num);
            if (plainAddr != num)
            {
                return "";
            }

            string comment;
            if (Program.CommentCache.TryGetValue(U.toOffset(plainAddr), out comment))
            {//ユーザー定義のコメントがある
                return comment;
            }

            AsmMapFile map = GetAsmMapFile();

            AsmMapFile.AsmMapSt a;
            if (!map.TryGetValue(U.toPointer(plainAddr), out a))
            {
                return "";
            }

            if (a.TypeName != "EVENT")
            {
                return "";
            }

            return a.Name;
        }
        //MAPファイルに定義されている定番のASMルーチン
        public bool IsFixedASM(uint num)
        {
            if (num <= 0)
            {
                return false;
            }

            num = U.toOffset(num);
            uint plainAddr = DisassemblerTrumb.ProgramAddrToPlain(num);

            AsmMapFile map = GetAsmMapFile();
            AsmMapFile.AsmMapSt a;
            if (!map.TryGetValue(U.toPointer(plainAddr), out a))
            {
                return false;
            }
            if (a.Length > 0)
            {//長さが判明しているということはおそらくASM関数ではない.
                return false;
            }
            if (plainAddr > Program.ROM.RomInfo.compress_image_borderline_address())
            {//画像ボーダーよりも向こう側
                return false;
            }

            //おそらくASM関数だと思われる
            return true;
        }
        public List<string> MakeNameList()
        {
            List<string> list = new List<string>();
            AsmMapFile map = GetAsmMapFile();

            foreach (var pair in map.GetAsmMap())
            {
                list.Add(pair.Value.Name);
            }

            return list;
        }

        public bool IsStopFlagOn()
        {
            return this.IsStopFlag;
        }
    }
}
