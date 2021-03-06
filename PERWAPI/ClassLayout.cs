/*  
 * PERWAPI - An API for Reading and Writing PE Files
 * 
 * Copyright (c) Diane Corney, Queensland University of Technology, 2004.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the PERWAPI Copyright as included with this
 * distribution in the file PERWAPIcopyright.rtf.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY as is explained in the copyright notice.
 *
 * The author may be contacted at d.corney@qut.edu.au
 * 
 * Version Date:  26/01/07
 */

using System;
using System.IO;
using System.Collections;
using System.Diagnostics.Contracts;
using System.Security.Cryptography;
using JetBrains.Annotations;


namespace QUT.PERWAPI
{
    /**************************************************************************/
    /// <summary>
    /// Layout information for a class (.class [sequential | explicit])
    /// </summary>
    internal class ClassLayout : MetaDataElement
    {
        private ClassDef parent;
        private ushort packSize = 0;
        private uint classSize = 0;
        private readonly uint parentIx = 0;

        /*-------------------- Constructors ---------------------------------*/

        internal ClassLayout(int pack, int cSize, ClassDef par)
            : this((ushort) pack, (uint) cSize, par)
        {
        }

        internal ClassLayout(ushort pack, uint cSize, ClassDef par)
        {
            packSize = pack;
            classSize = cSize;
            parent = par;
            tabIx = MDTable.ClassLayout;
        }

        internal ClassLayout(PEReader buff)
        {
            Contract.Requires(buff != null);
            packSize = buff.ReadUInt16();
            classSize = buff.ReadUInt32();
            parentIx = buff.GetIndex(MDTable.TypeDef);
            tabIx = MDTable.ClassLayout;
        }

        [CanBeNull]
        internal static ClassLayout FindLayout(PEReader buff, ClassDef paren, uint classIx)
        {
            Contract.Requires(buff != null);
            Contract.Requires(paren != null);
            buff.SetElementPosition(MDTable.ClassLayout, 0);
            for (int i = 0; i < buff.GetTableSize(MDTable.ClassLayout); i++)
            {
                ushort packSize = buff.ReadUInt16();
                uint classSize = buff.ReadUInt32();
                if (buff.GetIndex(MDTable.TypeDef) == classIx)
                    return new ClassLayout(packSize, classSize, paren);
            }
            return null;
        }

        internal static void Read(PEReader buff, TableRow[] layouts)
        {
            Contract.Requires(buff != null);
            Contract.Requires(layouts != null);
            for (int i = 0; i < layouts.Length; i++)
            {
                layouts[i] = new ClassLayout(buff);
            }
        }

        internal override void Resolve(PEReader buff)
        {
            parent = (ClassDef)buff.GetElement(MDTable.TypeDef, parentIx);
            if (parent != null) parent.Layout = this;
        }

        /*------------------------- public set and get methods --------------------------*/

        public void SetPack(int pack) { packSize = (ushort)pack; }
        public int GetPack() { return packSize; }
        public void SetSize(int size) { classSize = (uint)size; }
        public int GetSize() { return (int)classSize; }

        /*----------------------------- internal functions ------------------------------*/

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(tabIx, this);
        }

        internal static uint Size(MetaData md)
        {
            Contract.Requires(md != null);
            return 6 + md.TableIndexSize(MDTable.TypeDef);
        }

        internal sealed override void Write(PEWriter output)
        {
            output.Write(packSize);
            output.Write(classSize);
            output.WriteIndex(MDTable.TypeDef, parent.Row);
        }

    }



}