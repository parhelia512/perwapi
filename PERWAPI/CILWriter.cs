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
using System.Diagnostics;
using System.Collections;


namespace QUT.PERWAPI
{
    /**************************************************************************/
    // Class to Write CIL File
    /**************************************************************************/
    public class CILWriter : StreamWriter
    {
        private readonly PEFile pefile;
        private readonly ArrayList externRefs = new ArrayList();
        private FieldDef[] fields;
        private MethodDef[] methods;
        private ClassDef[] classes;

        public CILWriter(string filename, bool debug, PEFile pefile)
            : base(new FileStream(filename, FileMode.Create))
        {
            this.pefile = pefile;
            WriteLine("// ILASM output by PERWAPI");
            WriteLine("// for file <" + pefile.GetFileName() + ">");
        }

        internal void AddRef(ReferenceScope refScope)
        {
            if (!externRefs.Contains(refScope))
            {
                externRefs.Add(refScope);
            }
        }

        internal bool Debug { get; private set; }

        internal void BuildCILInfo()
        {
            fields = pefile.GetFields();
            methods = pefile.GetMethods();
            classes = pefile.GetClasses();
            if (fields != null)
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    fields[i].BuildCILInfo(this);
                }
            }
            if (methods != null)
            {
                for (int i = 0; i < methods.Length; i++)
                {
                    methods[i].BuildCILInfo(this);
                }
            }
            if (classes != null)
            {
                for (int i = 0; i < classes.Length; i++)
                {
                    classes[i].BuildCILInfo(this);
                }
            }
        }

        public void WriteFile(bool debug)
        {
            this.Debug = debug;
            for (int i = 0; i < externRefs.Count; i++)
            {
                ((ReferenceScope)externRefs[i]).Write(this);
            }
            Assembly assem = pefile.GetThisAssembly();
            if (assem != null)
            {
                assem.Write(this);
            }
            WriteLine(".module " + pefile.GetFileName());
            if (fields != null)
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    fields[i].Write(this);
                }
            }
            if (methods != null)
            {
                for (int i = 0; i < methods.Length; i++)
                {
                    methods[i].Write(this);
                }
            }
            if (classes != null)
            {
                for (int i = 0; i < classes.Length; i++)
                {
                    classes[i].Write(this);
                }
            }
            this.Flush();
            this.Close();
        }

    }
}