using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace QUT.PERWAPI
{
    /**************************************************************************/
    internal enum EHClauseType { Exception, Filter, Finally, Fault = 4 }

    internal class EHClause
    {
        private readonly EHClauseType clauseType;
        private readonly uint tryOffset;
        private readonly uint tryLength;
        private readonly uint handlerOffset;
        private readonly uint handlerLength;
        private uint filterOffset = 0;
        private MetaDataElement classToken = null;

        internal EHClause(EHClauseType cType, uint tOff, uint tLen, uint hOff, uint hLen)
        {
            clauseType = cType;
            tryOffset = tOff;
            tryLength = tLen;
            handlerOffset = hOff;
            handlerLength = hLen;
        }

        internal void ClassToken(MetaDataElement cToken)
        {
            Contract.Requires(cToken != null);
            classToken = cToken;
        }

        internal void FilterOffset(uint fOff)
        {
            filterOffset = fOff;
        }

        internal TryBlock MakeTryBlock(List<CILLabel> labels)
        {
            Contract.Requires(labels != null);
            Contract.Ensures(Contract.Result<TryBlock>() != null);
            TryBlock tBlock = new TryBlock(CILInstructions.GetLabel(labels, tryOffset),
                CILInstructions.GetLabel(labels, tryOffset + tryLength));
            CILLabel hStart = CILInstructions.GetLabel(labels, handlerOffset);
            CILLabel hEnd = CILInstructions.GetLabel(labels, handlerOffset + handlerLength);
            HandlerBlock handler = null;
            switch (clauseType)
            {
                case (EHClauseType.Exception):
                    handler = new Catch((Class)classToken, hStart, hEnd);
                    break;
                case (EHClauseType.Filter):
                    handler = new Filter(CILInstructions.GetLabel(labels, filterOffset), hStart, hEnd);
                    break;
                case (EHClauseType.Finally):
                    handler = new Finally(hStart, hEnd);
                    break;
                case (EHClauseType.Fault):
                    handler = new Fault(hStart, hEnd);
                    break;
            }
            tBlock.AddHandler(handler);
            return tBlock;
        }

    }
}
