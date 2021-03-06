﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

namespace FEBuilderGBA
{
    public partial class MoveToFreeSapceForm : Form
    {
        //実行完了後のコールバック
        Func<uint,uint,int> CallbackAfterRun = null;

        public MoveToFreeSapceForm()
        {
            InitializeComponent();

            //移動後に再スキャンをしないといけないので、時間のかかるスキャンは停止する
            Program.AsmMapFileAsmCache.StopRequest();

            this.NewDataInit.SelectedIndex = 0; //1行目でコピー
            this.DataPointerList.Items.Clear();
            this.TreatmentOldData.SelectedIndex = 0; //元データは 0クリア
            this.PaddingSpace.SelectedIndex = 1; //前方16バイトの余白
            this.CondFreeSpace.SelectedIndex = OptionForm.rom_extends_option();  // 0x09000000以降の拡張領域で、0x00がFREE_SPACE_SIZE個+必要データ数連続している領域
            this.FreeSpaceList.Items.Clear();
            this.CallbackAfterRun = null;

            InputFormRef.WriteButtonToYellow(this.RunButton, true);
            SimpleShowNewSize.ForeColor = OptionForm.Color_InputDecimal_ForeColor();
            SimpleShowDiffSize.ForeColor = OptionForm.Color_InputDecimal_ForeColor();
            ShowMoveSize.ForeColor = OptionForm.Color_InputDecimal_ForeColor();
            ShowNewSize.ForeColor = OptionForm.Color_InputDecimal_ForeColor();

            SimpleNewDataCount.Focus();
        }

        private void MoveToFreeSapceForm_Load(object sender, EventArgs e)
        {
        }

        public void FromInputFormRef(InputFormRef inputFormRef, Func<uint,uint,int> callbackAfterRun)
        {
            this.CallbackAfterRun = callbackAfterRun;
            if (inputFormRef.ReadStartAddress == null)
            {
                MoveAddress.Value = inputFormRef.BaseAddress;
            }
            else
            {
                MoveAddress.Value = inputFormRef.ReadStartAddress.Value;
            }
            MoveBlockSize.Value = inputFormRef.BlockSize;
            DataCount.Value = inputFormRef.DataCount;


            //テキトウなサイズに設定
            if (inputFormRef.DataCount <= 2)
            {
                NewDataCount.Value = 5;
            }
            else if (inputFormRef.DataCount <= 5)
            {
                NewDataCount.Value = 10;
            }
            else if (inputFormRef.DataCount <= 10)
            {
                NewDataCount.Value = 20;
            }
            else if (inputFormRef.DataCount <= 0xff)
            {
                NewDataCount.Value = 0xff;
            }
            else if (inputFormRef.DataCount <= 0x1000)
            {
                NewDataCount.Value = 0x1000;
            }
            else if (inputFormRef.DataCount <= 0x2000)
            {
                NewDataCount.Value = 0x2000;
            }
            else if (inputFormRef.DataCount <= 0x4000)
            {
                NewDataCount.Value = 0x4000;
            }
            else if (inputFormRef.DataCount <= 0x7fff)
            {
                NewDataCount.Value = 0x7fff;
            }
            else if (inputFormRef.DataCount <= 0xffff)
            {
                NewDataCount.Value = 0xffff;
            }
            else
            {
                NewDataCount.Value = inputFormRef.DataCount * 2;
            }

            uint expandsmax = inputFormRef.GetAddressListExpandsMax();
            if (expandsmax != U.NOT_FOUND)
            {//拡張上限が設定されている場合はそこで止める.
                if (NewDataCount.Value > expandsmax)
                {
                    NewDataCount.Value = expandsmax;
                }
                SimpleNewDataCount.Maximum = expandsmax;
                NewDataCount.Maximum = expandsmax;
            }

            //0件目が無効値だった場合2番目で埋める.
            NewDataInit.SelectedIndex = CalcFillDataOnListExpamds(inputFormRef);

            SearchDataButton_Click(this,new EventArgs());
            SearchFreespaceButton_Click(this, new EventArgs());

            SimpleNewDataCount.Focus();
        }
        int CalcFillDataOnListExpamds(InputFormRef inputFormRef)
        {
            if (inputFormRef.BaseAddress == 0)
            {//ベースアドレスが指定されていないので、何をしても無駄.
                return 0; //0番目で埋める
            }
            uint offsetBaseAddress = U.toOffset(inputFormRef.BaseAddress);
            if (!U.isSafetyOffset(offsetBaseAddress))
            {//ベースアドレスが危険
                return 0; //0番目で埋める
            }

            //現在のベースデータは有効ですか?
            if (!inputFormRef.IsDataExistsCallback(0, inputFormRef.BaseAddress))
            {//そもそも有効地ではないので、何もしない.
                return 0; //0番目で埋める
            }
            //現在のベースデータを2番目のデータとして見たとき有効ですか?
            if (!inputFormRef.IsDataExistsCallback(1, inputFormRef.BaseAddress))
            {//0番目にはnull値があるものと考えられる
                return 2; //1番目(1から数えて2番目)で埋める
            }
            return 0; //0番目で埋める
        }

        //影響を受けるデータの検索
        private void SearchDataButton_Click(object sender, EventArgs e)
        {
            if (InputFormRef.IsPleaseWaitDialog(this))
            {//2重割り込み禁止
                return;
            }
            using (InputFormRef.AutoPleaseWait pleaseWait = new InputFormRef.AutoPleaseWait(this))
            {
                uint moveAddress = (uint)MoveAddress.Value;

                List<uint> list = SearchPointer(moveAddress);

                this.DataPointerList.BeginUpdate();
                this.DataPointerList.Items.Clear();
                for (int i = 0; i < list.Count; i++)
                {
                    this.DataPointerList.Items.Add(list[i].ToString("X08"));
                }
                this.DataPointerList.EndUpdate();

                //全選択する
                for (int i = 0; i < this.DataPointerList.Items.Count; i++)
                {
                    this.DataPointerList.SetSelected(i, true);
                }
            }
        }

        static List<uint> GrepPointerAllOnEvent(uint needaddr)
        {
            needaddr = U.toOffset(needaddr);

            List<uint> ret = new List<uint>();
            if (needaddr <= 1)
            {
                Debug.Assert(false);
                return ret;
            }

            List<Address> list = U.MakeAllStructPointersList(true);
            U.AppendAllASMStructPointersList(list, null
                , isPatchInstallOnly: true
                , isPatchPointerOnly: true
                , isPatchStructOnly:  true
                , isPatchStoreSymbol: false
                , isUseOtherGraphics: false
                , isUseOAMSP: false
                );

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Addr == needaddr)
                {
                    if (U.isSafetyOffset(list[i].Pointer))
                    {
                        ret.Add(list[i].Pointer);
                    }
                }
            }
            return ret;
        }

        public static List<uint> SearchPointer(uint moveAddress)
        {
            List<uint> ret = new List<uint>();
            if (moveAddress == 0)
            {//新規確保だけ
                return ret;
            }

            moveAddress = U.toPointer(moveAddress);
            if (!U.isSafetyOffset(U.toOffset(moveAddress)))
            {
                string text = R.Error("空き領域のポインタまたはサイズが正しくありません。\r\n0x0 - 0x100までの領域は危険なので移動できません");
                string title = R._("エラー");
                MessageBox.Show(text
                    , title
                    , MessageBoxButtons.OK
                    , MessageBoxIcon.Error);
                return ret;
            }
            bool isFixedASM = Program.AsmMapFileAsmCache.IsFixedASM(moveAddress);
            if (isFixedASM)
            {
                DialogResult dr = R.ShowNoYes("このアドレス({0})は、プログラムコードを指しているようです。\r\n通常プログラムコードの領域をリポイントすることはありえません。\r\n本当にリポイントを継続してもよろしいですか？\r\n", U.ToHexString8(moveAddress));
                if (dr != DialogResult.Yes)
                {
                    return ret;
                }
            }

            InputFormRef.DoEvents(null, "U.GrepPointerAllOnLDR");
            ret = U.GrepPointerAllOnLDR(Program.ROM.Data, moveAddress);

            InputFormRef.DoEvents(null, "U.GrepPointerAllOnEvent");
            ret.AddRange(GrepPointerAllOnEvent(moveAddress));

            if (ret.Count <= 0)
            {
                InputFormRef.DoEvents(null,"U.GrepPointerAll");
                ret = U.GrepPointerAll(Program.ROM.Data, U.toPointer(moveAddress));
                if(ret.Count >= 2)
                {
                    DialogResult dr = R.ShowYesNo("LDRとイベントでは、該当するポインタ値が見つかりませんでした。\r\nROMをバイナリ検索をしたら、値が見つかりましたが、誤爆の可能性もあります。\r\nこの処理を続行してもよろしいですか?\r\n\r\n「はい」の場合、バイナリ検索でヒットした値を利用します。 \r\nそれ以外の場合は、見つからなかったとして処理します。");
                    if (dr != DialogResult.Yes)
                    {
                        ret = new List<uint>();
                        return ret;
                    }
                }
            }

            ret = U.Uniq(ret);
            return ret;
        }

        
        
        uint GetPaddingSpace()
        {
            //00=余白を作らない
            //01=移動後の領域には前方16バイトの余白を作る
            //02=移動後の領域には前方32バイトの余白を作る
            //03=移動後の領域には前方64バイトの余白を作る
            //04=移動後の領域には前方128バイトの余白を作る
            //05=移動後の領域には前方256バイトの余白を作る
            //06=移動後の領域には前方512バイトの余白を作る
            //07=移動後の領域には前方1024バイトの余白を作る
            switch (this.PaddingSpace.SelectedIndex)
            {
                case 0:
                    return 0;
                case 1:
                    return 16;
                case 2:
                    return 32;
                case 3:
                    return 64;
                case 4:
                    return 128;
                case 5:
                    return 256;
                case 6:
                    return 512;
                case 7:
                    return 1024;
            }
            return 16;
        }
        //空き領域の探索
        private void SearchFreespaceButton_Click(object sender, EventArgs e)
        {
            FreeSpaceList.Items.Clear();

            if (U.isSafetyOffset((uint)MoveAddress.Value))
            {
                //現在のデータが末尾の場合
                uint currentDataLength = (uint)(this.MoveBlockSize.Value * (DataCount.Value + 1) + MoveAddress.Value);
                if (currentDataLength == Program.ROM.Data.Length)
                {
                    uint p = (uint)this.MoveAddress.Value;
                    FreeSpaceList.Items.Add(p.ToString("X08") + " (FAST ALLOC)");
                }
            }

            uint newSize = (uint)(MoveBlockSize.Value * (NewDataCount.Value + 1)) + GetPaddingSpace();

            List<uint> list = SearchFreeSpace(newSize, CondFreeSpace.SelectedIndex);
            foreach (uint addr in list)
            {
                if (addr >= Program.ROM.Data.Length)
                {
                    FreeSpaceList.Items.Add(addr.ToString("X08") + " (FILE END)");
                }
                else
                {
                    FreeSpaceList.Items.Add(addr.ToString("X08"));
                }
            }

            //一つ見つけたから、先頭を選択.
            if (FreeSpaceList.Items.Count >= 1)
            {
                FreeSpaceList.SelectedIndex = 0;
            }
        }

        //空き領域の探索
        static List<uint> SearchFreeSpace(uint newSize, int condFreeSpace)
        {
            List<uint> addrList = new List<uint>();

            uint extendsArea = 0x01000000;
            if (Program.ROM.RomInfo.version() == 6)
            {
                extendsArea = 0x01000000 / 2;
            }

            uint addr;
            //00=ファイル末尾
            //01=0x09000000以降の拡張領域で、0x00が8192個+必要データ数連続している領域
            //02=0x09000000以降の拡張領域で、0xFFが8192個+必要データ数連続している領域
            //03=0x08000000以降の通常領域で、0x00が8192個+必要データ数連続している領域
            //04=0x08000000以降の通常領域で、0xFFが8192個+必要データ数連続している領域
            const int FREE_SPACE_SIZE = 8192;
            uint needSize = U.Padding4(FREE_SPACE_SIZE + newSize);

            if (condFreeSpace == 1)
            {
                for (addr = extendsArea; addr < Program.ROM.Data.Length; )
                {
                    addr = Program.ROM.FindFreeSpace(addr, 0x00, needSize);
                    if (addr == U.NOT_FOUND
                        || ImageUtilMagic.IsMagicArea(addr) == true)
                    {
                        break;
                    }
                    addrList.Add(addr);
                    addr = addr + needSize;

                    if (addrList.Count >= 10)
                    {
                        break;
                    }
                }
            }
            else if (condFreeSpace == 2)
            {
                for (addr = extendsArea; addr < Program.ROM.Data.Length; )
                {
                    addr = Program.ROM.FindFreeSpace(addr, 0xff, needSize);
                    if (addr == U.NOT_FOUND
                        || ImageUtilMagic.IsMagicArea(addr) == true)
                    {
                        break;
                    }
                    addrList.Add(addr);
                    addr = addr + needSize;

                    if (addrList.Count >= 10)
                    {
                        break;
                    }
                }
            }
            else if (condFreeSpace == 3)
            {
                for (addr = 0x0; addr < Program.ROM.Data.Length; )
                {
                    addr = Program.ROM.FindFreeSpace(addr, 0x00, needSize);
                    if (addr == U.NOT_FOUND
                        || ImageUtilMagic.IsMagicArea(addr) == true)
                    {
                        break;
                    }
                    addrList.Add(addr);
                    addr = addr + needSize;

                    if (addrList.Count >= 10)
                    {
                        break;
                    }
                }
            }
            else if (condFreeSpace == 4)
            {
                for (addr = 0x0; addr < Program.ROM.Data.Length; )
                {
                    addr = Program.ROM.FindFreeSpace(addr, 0xff, needSize);
                    if (addr == U.NOT_FOUND
                        || ImageUtilMagic.IsMagicArea(addr) == true)
                    {
                        break;
                    }
                    addrList.Add(addr);
                    addr = addr + needSize;

                    if (addrList.Count >= 10)
                    {
                        break;
                    }
                }
            }
            //末尾
            addr = U.Padding4((uint)Program.ROM.Data.Length);
            if (addr + newSize < 0x02000000)
            {//32MBを超えられないためチェック.
                addrList.Add(addr);
            }
            return addrList;
        }

        //空き領域の探索
        public static uint SearchFreeSpaceOne(uint newSize, int condFreeSpace)
        {
            uint extendsArea = 0x01000000;
            if (Program.ROM.RomInfo.version() == 6)
            {
                extendsArea = 0x01000000 / 2;
            }

            uint addr;
            //00=ファイル末尾
            //01=0x09000000以降の拡張領域で、0x00が8192個+必要データ数連続している領域
            //02=0x09000000以降の拡張領域で、0xFFが8192個+必要データ数連続している領域
            //03=0x08000000以降の通常領域で、0x00が8192個+必要データ数連続している領域
            //04=0x08000000以降の通常領域で、0xFFが8192個+必要データ数連続している領域
            const int LTRIM_SPACE_SIZE = 16;
            const int FREE_SPACE_SIZE = 8192;
            uint needSize = U.Padding4(FREE_SPACE_SIZE + newSize);
            if (condFreeSpace == 1)
            {
                addr = Program.ROM.FindFreeSpace(extendsArea, 0x00, needSize);
                if (addr != U.NOT_FOUND
                    && ImageUtilMagic.IsMagicArea(addr) == false)
                {
                    return addr + LTRIM_SPACE_SIZE;
                }
            }
            else if (condFreeSpace == 2)
            {
                addr = Program.ROM.FindFreeSpace(extendsArea, 0xff, needSize);
                if (addr != U.NOT_FOUND
                    && ImageUtilMagic.IsMagicArea(addr) == false)
                {
                    return addr + LTRIM_SPACE_SIZE;
                }
            }
            else if (condFreeSpace == 3)
            {
                addr = Program.ROM.FindFreeSpace(0x0, 0x00, needSize);
                if (addr != U.NOT_FOUND
                    && ImageUtilMagic.IsMagicArea(addr) == false)
                {
                    return addr + LTRIM_SPACE_SIZE;
                }
            }
            else if (condFreeSpace == 4)
            {
                addr = Program.ROM.FindFreeSpace(0x0, 0xff, needSize);
                if (addr != U.NOT_FOUND
                    && ImageUtilMagic.IsMagicArea(addr) == false)
                {
                    return addr + LTRIM_SPACE_SIZE;
                }
            }
            //末尾
            addr = U.Padding4((uint)Program.ROM.Data.Length);
            if (addr + newSize < 0x02000000)
            {//32MBを超えられないためチェック.
                return addr;
            }
            return U.NOT_FOUND;
        }


        //移動処理
        private void RunButton_Click(object sender, EventArgs e)
        {
            if (InputFormRef.IsPleaseWaitDialog(this))
            {
                R.ShowStopError(InputFormRef.GetBusyErrorExplain());
                return;
            }

            //もともとのアドレスが0の場合は確保だけやる.
            if (this.MoveAddress.Value == 0)
            {
                AllocOnly();
                return;
            }
            if (FreeSpaceList.SelectedIndex < 0)
            {
                //エラー 空き領域を選択してください。
                string text = R.Error("空き領域を選択してください");
                string title = R._("エラー");
                MessageBox.Show(text
                    , title
                    , MessageBoxButtons.OK
                    , MessageBoxIcon.Error);
                return;
            }

            if (this.DataPointerList.SelectedIndex < 0)
            {
                //エラー データポインタがありません。危険なので停止します.
                string text = R.Error("書き換えるポインタを一つは選択してください");
                string title = R._("エラー");
                MessageBox.Show(text
                    , title
                    , MessageBoxButtons.OK
                    , MessageBoxIcon.Error);
                return;
            }

            uint paddingSize = GetPaddingSpace();
            uint freeSpaceAddr = U.atoh(FreeSpaceList.Text);
            uint newSize = (uint)(MoveBlockSize.Value * (NewDataCount.Value + 1));
            if (freeSpaceAddr <= 0x100 || newSize <= 0)
            {//空き領域のアドレスの指定がおかしい.
                string text = R.Error("空き領域のポインタまたはサイズが正しくありません");
                string title = R._("エラー");
                MessageBox.Show(text
                    , title
                    , MessageBoxButtons.OK
                    , MessageBoxIcon.Error);
                return;
            }

            uint moveAddress = (uint)MoveAddress.Value;
            uint movesize = (uint)(MoveBlockSize.Value * (DataCount.Value) );
            if ( !U.isSafetyOffset(moveAddress) )
            {//データ元のアドレスの指定がおかしい.
                string text = R.Error("コピー元のアドレス指定が正しくありません");
                string title = R._("エラー");
                MessageBox.Show(text
                    , title
                    , MessageBoxButtons.OK
                    , MessageBoxIcon.Error);
                return;
            }

            {
                uint currentDataLength = (uint)(this.MoveBlockSize.Value * (DataCount.Value + 1) + MoveAddress.Value);
                if (currentDataLength == Program.ROM.Data.Length)
                {
                    uint p = (uint)this.MoveAddress.Value;
                    if (freeSpaceAddr == p)
                    {
                        //現在のデータが末尾の場合、移動させずにそのまま利用する FAST ALLOC
                        //移動させないため、元のデータをクリアしてはいけない
                        TreatmentOldData.SelectedIndex = 2;
                    }
                }
            }
            
            //移動前ポインタがおかしい.
            foreach (string item in this.DataPointerList.SelectedItems)
            {
                uint p = U.atoh(item);
                if (!U.isSafetyOffset(p))
                {
                    string text = R.Error("影響を受けるポインタが正しくありません");
                    string title = R._("エラー");
                    MessageBox.Show(text
                        , title
                        , MessageBoxButtons.OK
                        , MessageBoxIcon.Error);
                    return;
                }
            }


            Undo.UndoData undodata = Program.Undo.NewUndoData(this.Text);
            if (freeSpaceAddr + newSize + paddingSize> Program.ROM.Data.Length)
            {//ファイル末尾に追加するなどで、既存のデータ領域を超えてしまう場合は、データを延ばす.
                paddingSize = 0; //末尾に入れる場合 paddingいる？
                Program.ROM.write_resize_data(freeSpaceAddr + newSize + paddingSize);
            }
            if (freeSpaceAddr + paddingSize + movesize > Program.ROM.Data.Length)
            {//newSizeが元サイズより小さい場合???
                Program.ROM.write_resize_data(freeSpaceAddr + paddingSize + movesize);
            }

            //移動後の場所
            uint newDataAddr = freeSpaceAddr + paddingSize;
            //各種サイズ
            uint dataCount = (uint)(DataCount.Value);
            uint blockSize = (uint)(MoveBlockSize.Value);
            uint newDataCount = (uint)(NewDataCount.Value);
            //終端データ
            byte[] term_onedata = Program.ROM.getBinaryData(moveAddress + movesize, blockSize);

            //移動処理
            byte[] original =  Program.ROM.getBinaryData(moveAddress,movesize);
            Program.ROM.write_range(newDataAddr, original, undodata);

            if (NewDataInit.SelectedIndex == 2)
            {//02=新規確保部分はデータの2番目で埋める
                if (movesize <= blockSize * 2)
                {//サイズが足りないのでオーダーは実行不可能
                    NewDataInit.SelectedIndex = 0;
                }
                else
                {
                    byte[] top_seconddata = U.getBinaryData(original, blockSize, blockSize);
                    for (uint i = dataCount; i < newDataCount; i++)
                    {
                        Program.ROM.write_range(newDataAddr + (i * blockSize), top_seconddata);
                    }
                }
            }
            if (NewDataInit.SelectedIndex == 0)
            {//00=新規確保部分はデータの1番目で埋める
                if (movesize <= 0)
                {//サイズ0なのでオーダーは実行不可能
                }
                else
                {
                    byte[] top_onedata = U.getBinaryData(original, 0, blockSize);
                    for (uint i = dataCount; i < newDataCount; i++)
                    {
                        Program.ROM.write_range(newDataAddr + (i * blockSize), top_onedata);
                    }
                }
            }

            //もともとの終端データを末尾に移動.
            Program.ROM.write_range(newDataAddr + ((newDataCount) * blockSize), term_onedata);

            //ポインタの書き換え
            foreach (string item in this.DataPointerList.SelectedItems)
            {
                uint p = U.atoh(item);

                Program.ROM.write_p32(p, newDataAddr, undodata);
            }
            //etcデータの変更
            RepointEtcData(moveAddress, movesize, newDataAddr);

            //元データの処遇
            if (TreatmentOldData.SelectedIndex == 0)
            {//00=移動前のデータは0x00でクリア
                byte[] fill = U.FillArray(movesize, 0x00);
                Program.ROM.write_range(moveAddress, fill, undodata);
            }
            else if (TreatmentOldData.SelectedIndex == 0)
            {//01=移動前のデータは0xFFでクリア
                byte[] fill = U.FillArray(movesize, 0xff);
                Program.ROM.write_range(moveAddress, fill, undodata);
            }

            Program.Undo.Push(undodata);
            this.Close();

            if (this.CallbackAfterRun != null)
            {
                this.CallbackAfterRun(newDataAddr, (uint)NewDataCount.Value);
            }

        }

        //確保だけやる.
        private void AllocOnly()
        {
            if (FreeSpaceList.SelectedIndex < 0)
            {
                //エラー 空き領域を選択してください。
                string text = R.Error("空き領域を選択してください");
                string title = R._("エラー");
                MessageBox.Show(text
                    , title
                    , MessageBoxButtons.OK
                    , MessageBoxIcon.Error);
                return;
            }

            uint paddingSize = GetPaddingSpace();
            uint freeSpaceAddr = U.atoh(FreeSpaceList.Text);
            uint newSize = (uint)(MoveBlockSize.Value * (NewDataCount.Value + 1));
            if (freeSpaceAddr <= 0x100 || newSize <= 0)
            {//空き領域のアドレスの指定がおかしい.
                string text = R.Error("空き領域のポインタまたはサイズが正しくありません。0x0 - 0x100までの領域は危険なので移動できません");
                string title = R._("エラー");
                MessageBox.Show(text
                    , title
                    , MessageBoxButtons.OK
                    , MessageBoxIcon.Error);
                return;
            }


            Undo.UndoData undodata = Program.Undo.NewUndoData(this.Text);

            if (freeSpaceAddr + newSize + paddingSize > Program.ROM.Data.Length)
            {//ファイル末尾に追加するなどで、既存のデータ領域を超えてしまう場合は、データを延ばす.
                paddingSize = 0; //末尾に入れる場合 paddingいる？
                Program.ROM.write_resize_data(freeSpaceAddr + newSize + paddingSize);
            }

            //移動後の場所
            uint newDataAddr = freeSpaceAddr + paddingSize;

            uint dataCount = (uint)(MoveBlockSize.Value);
            uint newDataCount = (uint)(NewDataCount.Value);

            //確保処理
            Program.ROM.write_range(newDataAddr, U.FillArray(newSize, 0));

            Program.Undo.Push(undodata);
            this.Close();

            if (this.CallbackAfterRun != null)
            {
                this.CallbackAfterRun(newDataAddr, (uint)NewDataCount.Value);
            }

        }

        private void DataSize_ValueChanged(object sender, EventArgs e)
        {
            ShowMoveSize.Text = 
                (MoveBlockSize.Value * (DataCount.Value) ).ToString();
            ShowNewSize.Text =
                (MoveBlockSize.Value * (NewDataCount.Value)).ToString();
            SimpleShowNewSize.Text = ShowNewSize.Text;
            SimpleNewDataCount.Value = NewDataCount.Value;
            SimpleShowDiffSize.Text =
                U.ToPlus
                ((int)MoveBlockSize.Value * ((int)NewDataCount.Value - (int)DataCount.Value));
            SimpleDataCount.Text = DataCount.Value.ToString();
        }

        public static void RepointEtcData(uint oldAddr,uint oldSize , uint newAddr)
        {
            Program.LintCache.RepointEtcData(oldAddr, oldSize, newAddr);
            Program.CommentCache.RepointEtcData(oldAddr, oldSize, newAddr);
        }

        private void SimpleNewDataCount_ValueChanged(object sender, EventArgs e)
        {
            if (sender == SimpleNewDataCount )
            {
                NewDataCount.Value = SimpleNewDataCount.Value;
            }
        }
    }
}
