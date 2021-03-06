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

// If the compilation requires the use of the original
// SimpleWriter.dll
// then define the following symbol
// #define SIMPLEWRITER
// without this symbol, the code requires SymbolRW.

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Diagnostics.SymbolStore;
using QSy = QUT.Symbols;

namespace QUT.PERWAPI
{
    /**************************************************************************  
     * Classes related to PDB                                                 *
     **************************************************************************/

    #region PDB Classes

    /// <summary>
    /// Writes PDB files
    /// </summary>
    public class PDBWriter
    {
        private readonly ArrayList _docWriters = new ArrayList();
        private readonly ArrayList methods = new ArrayList();
        private Method currentMethod = null;
        private Scope currentScope = null;
        private byte[] debugInfo;
        private SymbolToken entryPoint;

        /// <summary>
        /// The name of the PE file this PDB file belongs to.
        /// </summary>
        public string PEFilename { get; }

        /// <summary>
        /// The name of the PDB file being written.
        /// </summary>
        public string PDBFilename
        {
            get
            {
                Contract.Ensures(Contract.Result<string>() != null);
                return Path.ChangeExtension(PEFilename, ".pdb");
            }
        }

        /// <summary>
        /// Provide access to the debug info which needs to be written to the PE file.
        /// This is only available after the call to WritePDBFile() has been made.
        /// </summary>
        public byte[] DebugInfo
        {
            get
            {
                if (debugInfo == null) throw new Exception("DeugInfo is only available after calling WritePDBFile()");
                return debugInfo;
            }
        }

        /// <summary>
        /// Create a new instance of the PDB Writer
        /// </summary>
        /// <param name="PEFilename">The name of the PE file we are writting the PDB file for.</param>
        public PDBWriter(string PEFilename)
        {
            Contract.Requires(PEFilename != null);
            this.PEFilename = PEFilename;
        }

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(PEFilename != null);
        }

        /// <summary>
        /// Set the entry method of the applicaiton
        /// </summary>
        /// <param name="token">The token for the entry method.</param>
        public void SetEntryPoint(int token)
        {
            entryPoint = new SymbolToken(token);
        }

        /// <summary>
        /// Open a new scope.
        /// </summary>
        /// <param name="offset">Offset as to where the scope should start.</param>
        public void OpenScope(int offset)
        {

            // Make sure we are in a method
            if (currentMethod == null)
                throw new Exception("You can not open a scope before opening a method.");

            // Create and add the new scope
            Scope scope = new Scope();
            scope.OffsetStart = offset;
            scope.ParentScope = currentScope;

            // Check if this is the first/root scope or a child scope.
            if (currentScope == null)
            {

                // Check to make sure we don't try to create two root scopes.
                if (currentMethod.Scope != null)
                    throw new Exception("Only one top-most scope is permitted.");

                currentMethod.Scope = scope;
            }
            else
            {
                currentScope.ChildScopes.Add(scope);
            }

            // Set the current scope
            currentScope = scope;

        }

        /// <summary>
        /// Close the current scope at the given offset.
        /// </summary>
        /// <param name="offset">The offset of where to close the scope.</param>
        public void CloseScope(int offset)
        {

            // Make sure a scope is open
            if (currentScope == null)
                throw new Exception("You can not close a scope now, none are open.");

            // Set the end offset for this scope and close it.
            currentScope.OffsetEnd = offset;
            currentScope = currentScope.ParentScope;

        }

        /// <summary>
        /// Bind a local to the current scope.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <param name="idx">The index of the variable in the locals table.</param>
        /// <param name="token">The symbol token for the given variable.</param>
        /// <param name="startOffset">The starting offset for the binding.  Set to 0 to default to current scope.</param>
        /// <param name="endOffset">The ending offset for the binding.  Set to 0 to default to current scope.</param>
        public void BindLocal(string name, int idx, uint token, int startOffset, int endOffset)
        {
            Contract.Requires(name != null);

            // Check to make sure a scope is open
            if (currentScope == null)
                throw new Exception("You must have an open scope in order to bind locals.");

            // Create the new local binding object
            LocalBinding lb = new LocalBinding();
            lb.Name = name;
            lb.Index = idx;
            lb.Token = new SymbolToken((int)token);
            lb.OffsetStart = startOffset;
            lb.OffsetEnd = endOffset;

            // Add to the current scope
            currentScope.Locals.Add(lb);

        }

        /// <summary>
        /// Adds a given ConstantBinding to the current scope.
        /// </summary>
        /// <param name="binding">The constant to add to this scope.</param>
        /* Not supported at this time.  Doesn't work correctly.  AKB 2007-02-03
        public void BindConstant(ConstantBinding binding) {

            // Check to make sure a scope is open
            if (currentScope == null)
                throw new Exception("You must have an open scope in order to bind a constant.");

            // Add the constants to the current scope
            currentScope.Constants.Add(binding);

        }
        */

        /// <summary>
        /// Add a new sequnce point.
        /// </summary>
        /// <param name="sourceFile">The source file the sequence point is in.</param>
        /// <param name="docLanguage">The language of the source file.</param>
        /// <param name="langVendor">The language vendor of the source file.</param>
        /// <param name="docType">The document type.</param>
        /// <param name="offset">The offset of the sequence point.</param>
        /// <param name="line">The starting line for the sequence point.</param>
        /// <param name="col">The starting column for the sequence point.</param>
        /// <param name="endLine">The ending line for the sequence point.</param>
        /// <param name="endCol">The ending column for the sequence point.</param>
        public void AddSequencePoint(string sourceFile, Guid docLanguage, Guid langVendor, Guid docType, int offset, int line, int col, int endLine, int endCol)
        {
            Contract.Requires(sourceFile != null);
            Document sourceDoc = null;

            // Make sure we are in a method
            if (currentMethod == null)
                throw new Exception("You can not add sequence points before opening a method.");

            // Check if a reference for this source document already exists
            foreach (Document doc in _docWriters)
                if (sourceFile == doc._file && docLanguage == doc._docLanguage && langVendor == doc._langVendor && docType == doc._docType)
                {
                    sourceDoc = doc;
                    break;
                }

            // If no existing document, create a new one
            if (sourceDoc == null)
            {
                sourceDoc = new Document();
                sourceDoc._file = sourceFile;
                sourceDoc._docLanguage = docLanguage;
                sourceDoc._langVendor = langVendor;
                sourceDoc._docType = docType;
                _docWriters.Add(sourceDoc);
            }

            SequencePointList spList = (SequencePointList)currentMethod.SequencePointList[sourceDoc];

            if (spList == null)
                currentMethod.SequencePointList.Add(sourceDoc, spList = new SequencePointList());

            spList.offsets.Add(offset);
            spList.lines.Add(line);
            spList.cols.Add(col);
            spList.endLines.Add(endLine);
            spList.endCols.Add(endCol);
        }

        /// <summary>
        /// Open a method.  Scopes and sequence points will be added to this method.
        /// </summary>
        /// <param name="token">The token for this method.</param>
        public void OpenMethod(int token)
        {

            // Add this new method to the list of methods
            Method meth = new Method();
            meth.Token = new SymbolToken(token);
            methods.Add(meth);

            // Set the current method
            currentMethod = meth;

        }

        /// <summary>
        /// Close the current method.
        /// </summary>
        public void CloseMethod()
        {

            // Make sure a method is open
            if (currentMethod == null)
                throw new Exception("No methods currently open.");

            // Check to make sure all scopes have been closed.
            if (currentScope != null)
                throw new Exception("Can not close method until all scopes are closed.  Method Token: " + currentMethod.Token.ToString());

            // Change the current method to null
            currentMethod = null;

        }

        /// <summary>
        /// Write the PDB file to disk.
        /// </summary>
        public void WritePDBFile()
        {

            /* 
             * Write default template PDB file first
             * 
             * NOTE: This is a dodgy hack so please feel free to change! AKB 06-01-2007
             * 
             * For some reason if there isn't a PDB file to start with the
             * debugger used to step through the resulting PDB file will 
             * jump all over the place.  Resulting in incorrect step-throughs.
             * I have not been able to work out why yet but I think it has
             * something to do with the call to GetWriterForFile().
             * Also, it doesn't happen on all PDB files.
             * It is interesting to note that if it is writting a debug file 
             * to go with a PE file compiled by csc (MS Compiler) it works fine.
             */

            // Get the blank PDB file from the resource assembly
            System.Reflection.Assembly currentAssembly = System.Reflection.Assembly.GetExecutingAssembly();
            Stream blankPDB = currentAssembly.GetManifestResourceStream("QUT.PERWAPI.Blank.pdb");

            // Write the blank PDB file to the disk
            using (FileStream fs = new FileStream(PDBFilename, FileMode.OpenOrCreate, FileAccess.Write))
            {
                BinaryWriter bw = new BinaryWriter(fs);

                // Loop through the PDB file and write it to the disk
                byte[] buffer = new byte[32768];
                while (true)
                {
                    int read = blankPDB.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break;
                    bw.Write(buffer, 0, read);
                }

                // Close all of the streams we have opened
                bw.Close();
                fs.Close();
            }

            // Create the new Symbol Writer
            QSy.SymbolWriter symWriter = new QSy.SymbolWriter(PEFilename, PDBFilename);
            // Add each of the source documents
            foreach (Document doc in _docWriters)
            {
#if SIMPLEWRITER
                doc._docWriter = symWriter.DefineDocument(
                    doc._file,
                    doc._docLanguage,
                    doc._langVendor,
                    doc._docType
                );
                // Set the entry point if it exists
                if (entryPoint.GetToken() != 0)
                    symWriter.SetUserEntryPoint(entryPoint.GetToken());
#else 
                doc.docWriter = symWriter.DefineDocument(
                    doc._file,
                    ref doc._docLanguage,
                    ref doc._langVendor,
                    ref doc._docType
                );
                // Set the entry point if it exists
                if (entryPoint.GetToken() != 0)
                    symWriter.SetUserEntryPoint(entryPoint);
#endif // SIMPLEWRITER
            }
            // Loop through and add each method
            foreach (Method meth in methods)
            {
#if SIMPLEWRITER
                symWriter.OpenMethod(meth.Token.GetToken());
#else
                symWriter.OpenMethod(meth.Token);
#endif
                // Write the scope and the locals
                if (meth.Scope != null) WriteScopeAndLocals(symWriter, meth.Scope);

                // Add each of the sequence points
                foreach (Document sourceDoc in meth.SequencePointList.Keys)
                {
                    SequencePointList spList = (SequencePointList)meth.SequencePointList[sourceDoc];
#if SIMPLEWRITER
                    symWriter.DefineSequencePoints(sourceDoc._docWriter,
                        (uint[])spList.offsets.ToArray(),
                        (uint[])spList.lines.ToArray(),
                        (uint[])spList.cols.ToArray(),
                        (uint[])spList.endLines.ToArray(),
                        (uint[])spList.endCols.ToArray());
#else
                    symWriter.DefineSequencePoints(sourceDoc.docWriter,
                        spList.offsets.ToArray(),
                        spList.lines.ToArray(),
                        spList.cols.ToArray(),
                        spList.endLines.ToArray(),
                        spList.endCols.ToArray());
#endif // SIMPLEWRITER
                }
                symWriter.CloseMethod();
            }

            // Get the debug info
            debugInfo = symWriter.GetDebugInfo();
            // Close the PDB file
            symWriter.Close();

        }

        /// <summary>
        /// Write out the scopes and the locals to the PDB file.
        /// </summary>
        /// <param name="symWriter">The symbol writer for this file.</param>
        /// <param name="scope">The scope to write out.</param>
        private static void WriteScopeAndLocals(QSy.SymbolWriter symWriter, Scope scope)
        {
            // Open the scope
            symWriter.OpenScope(scope.OffsetStart);

            // Add each local variable
            foreach (LocalBinding lb in scope.Locals)
            {
                symWriter.DefineLocalVariable2(
                    lb.Name,
                    0,
#if SIMPLEWRITER
                    lb.Token.GetToken(),
#else
                    lb.Token,              
#endif
                    1,
                    lb.Index,
                    0,
                    0,
                    lb.OffsetStart,
                    lb.OffsetEnd
                );
            }

            // Add each constants
            /* For now don't add constants.  Doesn't work. AKB 09-01-2007
            foreach (ConstantBinding cb in scope.Constants) {
                symWriter.DefineConstant(
                    cb.Name,
                    cb.Value,
                    cb.GetSig()
                );
            }
            */

            // Add any child scopes
            foreach (Scope childScope in scope.ChildScopes)
                WriteScopeAndLocals(symWriter, childScope);

            // Close the scope
            symWriter.CloseScope(scope.OffsetEnd);

        }

        /// <summary>
        /// A list of sequence points.
        /// </summary>
        private class SequencePointList
        {
            internal readonly List<int> offsets = new List<int>();
            internal readonly List<int> lines = new List<int>();
            internal readonly List<int> cols = new List<int>();
            internal readonly List<int> endLines = new List<int>();
            internal readonly List<int> endCols = new List<int>();
        }

        /// <summary>
        /// A source file document.
        /// </summary>
        private class Document
        {
            internal string _file;
            internal Guid _docLanguage, _langVendor, _docType;
#if SIMPLEWRITER
            internal ulong _docWriter;
#else
            internal object docWriter;
#endif
        }

        /// <summary>
        /// A method.
        /// </summary>
        private class Method
        {
            internal SymbolToken Token;
            internal Scope Scope = null;
            internal readonly Hashtable SequencePointList = new Hashtable();
        }

        /// <summary>
        /// A scope.
        /// </summary>
        private class Scope
        {
            internal int OffsetStart;
            internal int OffsetEnd;
            internal Scope ParentScope = null;
            internal readonly List<LocalBinding> Locals = new List<LocalBinding>();
            // FIXME dead internal ArrayList Constants = new ArrayList();
            internal readonly List<Scope> ChildScopes = new List<Scope>();
        }

        /// <summary>
        /// A local binding.
        /// </summary>
        private class LocalBinding
        {
            internal string Name;
            internal int Index;
            internal SymbolToken Token;
            internal int OffsetStart;
            internal int OffsetEnd;
                Contract.Requires(name != null);

            [ContractInvariantMethod]
            private void ObjectInvariant()
            {
                Contract.Invariant(Name != null);
            }
        }

    }

    /// <summary>
    /// Read a given PDB file.
    /// </summary>
    public class PDBReader
    {
        //private static Guid IID_IMetaDataImport = new Guid("7DAC8207-D3AE-4c75-9B67-92801A497D44");
        //private static Guid CLSID_CorSymBinder = new Guid("AA544D41-28CB-11d3-BD22-0000F80849BD");
        private readonly ISymbolReader _reader;
        private string _fileName;

        /// <summary>
        /// Read the given PDB file by filename.
        /// </summary>
        /// <param name="fileName">The filename and path to the PDB file.</param>
        public PDBReader(string fileName)
        {
            Contract.Requires(fileName != null);
            _reader = (ISymbolReader)(new QSy.SymbolReader(fileName));
            _fileName = fileName;
        }

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(_reader != null);
            Contract.Invariant(_fileName != null);
        }

        /// <summary>
        /// Return a particular method.
        /// </summary>
        /// <param name="token">The token to identify the method.</param>
        /// <returns>The method with the given token.</returns>
        public PDBMethod GetMethod(int token)
        {
            try
            {
                ISymbolMethod method = _reader.GetMethod(new SymbolToken(token));

                if (method != null)
                    return new PDBMethod(method);
                else
                    return null;
            }
            catch
            {
                return null; // call fails on tokens which are not referenced
            }
        }

    }

    /// <summary>
    /// Defines debug information for a method.
    /// </summary>
    public class PDBMethod
    {
        private readonly ISymbolMethod _meth;

        /// <summary>
        /// Create a new PDB method object from an ISymbolMethod object.
        /// </summary>
        /// <param name="meth">The ISymbolMethod object to wrap.</param>
        internal PDBMethod(ISymbolMethod meth)
        {
            Contract.Requires(meth != null);
            _meth = meth;
        }

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(_meth != null);
            Contract.Invariant(SequencePoints != null);
        }

        /// <summary>
        /// The root scope of the method.
        /// </summary>
        public PDBScope Scope
        {
            get
            {
                Contract.Ensures(Contract.Result<PDBScope>() != null);
                return new PDBScope(_meth.RootScope);
            }
        }

        /// <summary>
        /// The sequence points in the method.
        /// </summary>
        public PDBSequencePoint[] SequencePoints
        {
            get
            {
                int spCount = _meth.SequencePointCount;
                int[] offsets = new int[spCount];
                ISymbolDocument[] documents = new ISymbolDocument[spCount];
                int[] lines = new int[spCount];
                int[] cols = new int[spCount];
                int[] endLines = new int[spCount];
                int[] endCols = new int[spCount];

                _meth.GetSequencePoints(offsets, documents, lines, cols, endLines, endCols);
                PDBSequencePoint[] spList = new PDBSequencePoint[spCount];

                for (int i = 0; i < spCount; i++)
                    spList[i] = new PDBSequencePoint(offsets[i], new PDBDocument(documents[i]), lines[i], cols[i], endLines[i], endCols[i]);

                return spList;
            }
        }

    }

    /// <summary>
    /// Defines a scope in which local variables exist.
    /// </summary>
    public class PDBScope
    {
        private readonly ISymbolScope _scope;

        /// <summary>
        /// Create a new scope from a ISymbolScope
        /// </summary>
        /// <param name="scope"></param>
        internal PDBScope(ISymbolScope scope)
        {
            Contract.Requires(scope != null);
            _scope = scope;
        }

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(_scope != null);
        }

        /// <summary>
        /// The starting index for the scope.
        /// </summary>
        public int StartOffset
        {
            get
            {
                return _scope.StartOffset;
            }
        }

        /// <summary>
        /// The end index for the scope.
        /// </summary>
        public int EndOffset
        {
            get
            {
                return _scope.EndOffset;
            }
        }

        /// <summary>
        /// The variables that exist in this scope.
        /// </summary>
        public PDBVariable[] Variables
        {
            get
            {
                List<PDBVariable> vars = new List<PDBVariable>();
                foreach (ISymbolVariable var in _scope.GetLocals())
                    vars.Add(new PDBVariable(var));

                return vars.ToArray();
            }
        }

        /// <summary>
        /// The sub-scopes within this scope.
        /// </summary>
        public PDBScope[] Children
        {
            get
            {
                List<PDBScope> children = new List<PDBScope>();
                foreach (ISymbolScope child in _scope.GetChildren())
                    children.Add(new PDBScope(child));

                return children.ToArray();
            }
        }

    }

    /// <summary>
    /// Defines a reference to one section of code to be highlighted when 
    /// stepping through in debug mode.  Typically one line of code.
    /// </summary>
    public class PDBSequencePoint
    {
        /// <summary>
        /// Create a new sequence point.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="doc">The source file.</param>
        /// <param name="line">The line the point begins on.</param>
        /// <param name="col">The column the point begins with.</param>
        /// <param name="endLine">The line the point ends on.</param>
        /// <param name="endCol">The column the point ends with.</param>
        internal PDBSequencePoint(int offset, PDBDocument doc, int line, int col, int endLine, int endCol)
        {
            Contract.Requires(doc != null);
            Offset = offset;
            Document = doc;
            Line = line;
            Column = col;
            EndLine = endLine;
            EndColumn = endCol;
        }

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(Document != null);
        }

        /// <summary>
        /// The source file for this sequence point.
        /// </summary>
        public PDBDocument Document { get; }

        /// <summary>
        /// 
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// The line this sequence point starts on.
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// The column this sequnce point starts with.
        /// </summary>
        public int Column { get; }

        /// <summary>
        /// The line this sequence point ends with.
        /// </summary>
        public int EndLine { get; }

        /// <summary>
        /// The column this sequence point ends with.
        /// </summary>
        public int EndColumn { get; }
    }

    /// <summary>
    /// A PDB variable object.  Stores debug information about a variable.
    /// </summary>
    public class PDBVariable
    {
        private readonly ISymbolVariable _var;

        /// <summary>
        /// Create a new PDBVariable object from an ISymbolVariable object.
        /// </summary>
        /// <param name="var"></param>
        internal PDBVariable(ISymbolVariable var)
        {
            Contract.Requires(var != null);
            _var = var;
        }

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(_var != null);
        }

        /// <summary>
        /// The name of the variable.
        /// </summary>
        public string Name
        {
            get
            {
                Contract.Ensures(Contract.Result<string>() != null);
                return _var.Name;
            }
        }

        /// <summary>
        /// The address or index of the variable.
        /// </summary>
        public int Address
        {
            get
            {
                return _var.AddressField1;
            }
        }

    }

    /// <summary>
    /// A PDB document is a source file.
    /// </summary>
    public class PDBDocument
    {
        private readonly ISymbolDocument _doc;

        /// <summary>
        /// Create a new document object from an existing document.
        /// </summary>
        /// <param name="doc">The ISymbolDocument to wrap.</param>
        internal PDBDocument(ISymbolDocument doc)
        {
            Contract.Requires(doc != null);
            _doc = doc;
        }

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(_doc != null);
        }

        /// <summary>
        /// The language for this document.
        /// </summary>
        public Guid Language
        {
            get
            {
                return _doc.Language;
            }
        }

        /// <summary>
        /// The language vendor for this document.
        /// </summary>
        public Guid LanguageVendor
        {
            get
            {
                return _doc.LanguageVendor;
            }
        }

        /// <summary>
        /// The type for this document.
        /// </summary>
        public Guid DocumentType
        {
            get
            {
                return _doc.DocumentType;
            }
        }

        /// <summary>
        /// The path/url to the source file.
        /// </summary>
        public string URL
        {
            get
            {
                return _doc.URL;
            }
        }

    }

    /**************************************************************************/
    // Added to enable PDB reading

    internal class MergeBuffer
    {
        private readonly CILInstruction[] _buffer;
        private readonly List<CILInstruction> _debugBuffer;
        private int _current;

        public MergeBuffer(CILInstruction[] buffer)
        {
            _debugBuffer = new List<CILInstruction>();
            Contract.Requires(buffer != null);
            _buffer = buffer;
        }

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(_buffer != null);
            Contract.Invariant(_debugBuffer != null);
            Contract.Invariant(_current >= 0);
        }

        public void Add(CILInstruction inst, uint offset)
        {
            Contract.Requires(inst != null);
            while (_current < _buffer.Length && _buffer[_current].offset < offset)
                _debugBuffer.Add(_buffer[_current++]);
            if (_debugBuffer.Count > 0 && offset >= ((CILInstruction)_debugBuffer[_debugBuffer.Count - 1]).offset)
            {
                inst.offset = offset;
                _debugBuffer.Add(inst);
            }
            else
            {
                int i;
                for (i = 0; i < _debugBuffer.Count; i++)
                    if (_debugBuffer[i].offset > offset)
                        break;
                inst.offset = offset;
                _debugBuffer.Insert((i > 0 ? i - 1 : i), inst);
            }
        }

        /// <summary>
        /// Tests if Instructions begin and end with an OpenScope/CloseScope pair
        /// </summary>
        /// <returns>True if there is a root scope</returns>
        public bool hasRootScope()
        {
            return (_debugBuffer.Count > 0 && _debugBuffer[0] is OpenScope);
        }

        public CILInstruction[] Instructions
        {
            get
            {
                while (_current < _buffer.Length)
                    _debugBuffer.Add(_buffer[_current++]);
                return _debugBuffer.ToArray();
            }
        }
    }


    #endregion
    /**************************************************************************/  

}
