﻿/*
Poor Man's T-SQL Formatter - a small free Transact-SQL formatting 
library for .Net 2.0, written in C#. 
Copyright (C) 2011 Tao Klerks

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using PoorMansTSqlFormatterLib.Interfaces;

namespace PoorMansTSqlFormatterLib.Formatters
{
    public class TSqlStandardFormatter : Interfaces.ISqlTreeFormatter
    {

        public TSqlStandardFormatter() : this("\t", 4, 999, true, false, false, true, true, true, false, true, false, false) { }

        public TSqlStandardFormatter(string indentString, int spacesPerTab, int maxLineWidth, bool expandCommaLists, bool trailingCommas, bool spaceAfterExpandedComma, bool expandBooleanExpressions, bool expandCaseStatements, bool expandBetweenConditions, bool breakJoinOnSections, bool uppercaseKeywords, bool htmlColoring, bool keywordStandardization)
        {
            IndentString = indentString;
            SpacesPerTab = spacesPerTab;
            MaxLineWidth = maxLineWidth;
            ExpandCommaLists = expandCommaLists;
            TrailingCommas = trailingCommas;
            SpaceAfterExpandedComma = spaceAfterExpandedComma;
            ExpandBooleanExpressions = expandBooleanExpressions;
            ExpandBetweenConditions = expandBetweenConditions;
            ExpandCaseStatements = expandCaseStatements;
            UppercaseKeywords = uppercaseKeywords;
            BreakJoinOnSections = breakJoinOnSections;
            HTMLColoring = htmlColoring;
            if (keywordStandardization)
                KeywordMapping = StandardKeywordRemapping.Instance;
            ErrorOutputPrefix = Interfaces.MessagingConstants.FormatErrorDefaultMessage + Environment.NewLine;
        }

        private string _indentString;
        public string IndentString
        {
            get
            {
                return _indentString;
            }
            set
            {
                _indentString = value.Replace("\\t", "\t");
            }
        }

        public IDictionary<string, string> KeywordMapping = new Dictionary<string, string>();

        public int SpacesPerTab { get; set; }
        public int MaxLineWidth { get; set; }
        public bool ExpandCommaLists { get; set; }
        public bool TrailingCommas { get; set; }
        public bool SpaceAfterExpandedComma { get; set; }
        public bool ExpandBooleanExpressions { get; set; }
        public bool ExpandCaseStatements { get; set; }
        public bool ExpandBetweenConditions { get; set; }
        public bool UppercaseKeywords { get; set; }
        public bool BreakJoinOnSections { get; set; }
        public bool HTMLColoring { get; set; }

        public bool HTMLFormatted { get { return HTMLColoring; } }
        public string ErrorOutputPrefix { get; set; }

        public string FormatSQLTree(XmlDocument sqlTreeDoc)
        {
            //thread-safe - each call to FormatSQLTree() gets its own independent state object
            TSqlFormattingState state = new TSqlFormattingState(HTMLColoring, IndentString, SpacesPerTab, MaxLineWidth, 0);

            if (sqlTreeDoc.SelectSingleNode(string.Format("/{0}/@{1}[.=1]", SqlXmlConstants.ENAME_SQL_ROOT, SqlXmlConstants.ANAME_ERRORFOUND)) != null)
                state.AddOutputContent(ErrorOutputPrefix);

            XmlNodeList rootList = sqlTreeDoc.SelectNodes(string.Format("/{0}/*", SqlXmlConstants.ENAME_SQL_ROOT));
            ProcessSqlNodeList(rootList, state);
            WhiteSpace_BreakAsExpected(state);

            return state.DumpOutput();
        }

        private void ProcessSqlNodeList(XmlNodeList rootList, TSqlFormattingState state)
        {
            foreach (XmlElement contentElement in rootList)
                ProcessSqlNode(contentElement, state);
        }

        private void ProcessSqlNode(XmlElement contentElement, TSqlFormattingState state)
        {
            int initialIndent = state.IndentLevel;

            switch (contentElement.Name)
            {
                case SqlXmlConstants.ENAME_SQL_STATEMENT:
                    WhiteSpace_SeparateStatements(contentElement, state);
                    state.ResetKeywords();
                    ProcessSqlNodeList(contentElement.SelectNodes("*"), state);
                    state.StatementBreakExpected = true;
                    break;

                case SqlXmlConstants.ENAME_SQL_CLAUSE:
                    state.UnIndentInitialBreak = true;
                    ProcessSqlNodeList(contentElement.SelectNodes("*"), state.IncrementIndent());
                    state.DecrementIndent();
                    state.BreakExpected = true;
                    break;

                case SqlXmlConstants.ENAME_SET_OPERATOR_CLAUSE:
                    state.DecrementIndent();
                    state.WhiteSpace_BreakToNextLine(); //this is the one already recommended by the start of the clause
                    state.WhiteSpace_BreakToNextLine(); //this is the one we additionally want to apply
                    ProcessSqlNodeList(contentElement.SelectNodes("*"), state.IncrementIndent());
                    state.BreakExpected = true;
                    state.AdditionalBreakExpected = true;
                    break;

                case SqlXmlConstants.ENAME_BATCH_SEPARATOR:
                    //newline regardless of whether previous element recommended a break or not.
                    state.WhiteSpace_BreakToNextLine();
                    ProcessSqlNodeList(contentElement.SelectNodes("*"), state);
                    state.BreakExpected = true;
                    break;

                case SqlXmlConstants.ENAME_DDL_PROCEDURAL_BLOCK:
                case SqlXmlConstants.ENAME_DDL_OTHER_BLOCK:
                case SqlXmlConstants.ENAME_CURSOR_DECLARATION:
                case SqlXmlConstants.ENAME_BEGIN_TRANSACTION:
                case SqlXmlConstants.ENAME_SAVE_TRANSACTION:
                case SqlXmlConstants.ENAME_COMMIT_TRANSACTION:
                case SqlXmlConstants.ENAME_ROLLBACK_TRANSACTION:
                case SqlXmlConstants.ENAME_CONTAINER_OPEN:
                case SqlXmlConstants.ENAME_CONTAINER_CLOSE:
                case SqlXmlConstants.ENAME_WHILE_LOOP:
                case SqlXmlConstants.ENAME_IF_STATEMENT:
                case SqlXmlConstants.ENAME_SELECTIONTARGET:
                case SqlXmlConstants.ENAME_CONTAINER_GENERALCONTENT:
                case SqlXmlConstants.ENAME_CTE_WITH_CLAUSE:
                case SqlXmlConstants.ENAME_PERMISSIONS_BLOCK:
                case SqlXmlConstants.ENAME_PERMISSIONS_DETAIL:
                case SqlXmlConstants.ENAME_MERGE_CLAUSE:
                case SqlXmlConstants.ENAME_MERGE_TARGET:
                    ProcessSqlNodeList(contentElement.SelectNodes("*"), state);
                    break;

                case SqlXmlConstants.ENAME_CASE_INPUT:
                case SqlXmlConstants.ENAME_BOOLEAN_EXPRESSION:
                case SqlXmlConstants.ENAME_BETWEEN_LOWERBOUND:
                case SqlXmlConstants.ENAME_BETWEEN_UPPERBOUND:
                    WhiteSpace_SeparateWords(state);
                    ProcessSqlNodeList(contentElement.SelectNodes("*"), state);
                    break;

                case SqlXmlConstants.ENAME_CONTAINER_SINGLESTATEMENT:
                case SqlXmlConstants.ENAME_CONTAINER_MULTISTATEMENT:
                case SqlXmlConstants.ENAME_MERGE_ACTION:
                    state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.SelectNodes("*"), state);
                    state.StatementBreakExpected = false; //the responsibility for breaking will be with the OUTER statement; there should be no consequence propagating out from statements in this container;
                    state.UnIndentInitialBreak = false; //if there was no word spacing after the last content statement's clause starter, doesn't mean the unIndent should propagate to the following content!
                    break;

                case SqlXmlConstants.ENAME_PERMISSIONS_TARGET:
                case SqlXmlConstants.ENAME_PERMISSIONS_RECIPIENT:
                case SqlXmlConstants.ENAME_DDL_WITH_CLAUSE:
                case SqlXmlConstants.ENAME_MERGE_CONDITION:
                case SqlXmlConstants.ENAME_MERGE_THEN:
                    state.BreakExpected = true;
                    state.UnIndentInitialBreak = true;
                    ProcessSqlNodeList(contentElement.SelectNodes("*"), state.IncrementIndent());
                    state.DecrementIndent();
                    break;

                case SqlXmlConstants.ENAME_JOIN_ON_SECTION:
                    if (BreakJoinOnSections)
                        state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CONTAINER_OPEN), state);
                    if (BreakJoinOnSections)
                        state.IncrementIndent();
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CONTAINER_GENERALCONTENT), state);
                    if (BreakJoinOnSections)
                        state.DecrementIndent();
                    break;

                case SqlXmlConstants.ENAME_CTE_ALIAS:
                    state.UnIndentInitialBreak = true;
                    ProcessSqlNodeList(contentElement.SelectNodes("*"), state);
                    break;

                case SqlXmlConstants.ENAME_ELSE_CLAUSE:
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CONTAINER_OPEN), state.DecrementIndent());
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CONTAINER_SINGLESTATEMENT), state.IncrementIndent());
                    break;

                case SqlXmlConstants.ENAME_DDL_AS_BLOCK:
                case SqlXmlConstants.ENAME_CURSOR_FOR_BLOCK:
                    state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CONTAINER_OPEN), state.DecrementIndent());
                    state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CONTAINER_GENERALCONTENT), state);
                    state.IncrementIndent();
                    break;

                case SqlXmlConstants.ENAME_TRIGGER_CONDITION:
                    state.DecrementIndent();
                    state.WhiteSpace_BreakToNextLine();
                    ProcessSqlNodeList(contentElement.SelectNodes("*"), state.IncrementIndent());
                    break;

                case SqlXmlConstants.ENAME_CURSOR_FOR_OPTIONS:
                case SqlXmlConstants.ENAME_CTE_AS_BLOCK:
                    state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CONTAINER_OPEN), state.DecrementIndent());
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CONTAINER_GENERALCONTENT), state.IncrementIndent());
                    break;

                case SqlXmlConstants.ENAME_DDL_RETURNS:
                case SqlXmlConstants.ENAME_MERGE_USING:
                case SqlXmlConstants.ENAME_MERGE_WHEN:
                    state.BreakExpected = true;
                    state.UnIndentInitialBreak = true;
                    ProcessSqlNodeList(contentElement.SelectNodes("*"), state);
                    break;

                case SqlXmlConstants.ENAME_BETWEEN_CONDITION:
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CONTAINER_OPEN), state);
                    state.IncrementIndent();
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_BETWEEN_LOWERBOUND), state.IncrementIndent());
                    if (ExpandBetweenConditions)
                        state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CONTAINER_CLOSE), state.DecrementIndent());
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_BETWEEN_UPPERBOUND), state.IncrementIndent());
                    state.DecrementIndent();
                    state.DecrementIndent();
                    break;

                case SqlXmlConstants.ENAME_DDLDETAIL_PARENS:
                case SqlXmlConstants.ENAME_FUNCTION_PARENS:
                    //simply process sub-nodes - don't add space or expect any linebreaks (but respect linebreaks if necessary)
                    state.WordSeparatorExpected = false;
                    WhiteSpace_BreakAsExpected(state);
                    state.AddOutputContent(FormatOperator("("), Interfaces.SqlHtmlConstants.CLASS_OPERATOR);
                    ProcessSqlNodeList(contentElement.SelectNodes("*"), state.IncrementIndent());
                    state.DecrementIndent();
                    WhiteSpace_BreakAsExpected(state);
                    state.AddOutputContent(FormatOperator(")"), Interfaces.SqlHtmlConstants.CLASS_OPERATOR);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlXmlConstants.ENAME_DDL_PARENS:
                case SqlXmlConstants.ENAME_EXPRESSION_PARENS:
                case SqlXmlConstants.ENAME_SELECTIONTARGET_PARENS:
                    WhiteSpace_SeparateWords(state);
                    if (contentElement.Name.Equals(SqlXmlConstants.ENAME_EXPRESSION_PARENS))
                        state.IncrementIndent();
                    state.AddOutputContent(FormatOperator("("), Interfaces.SqlHtmlConstants.CLASS_OPERATOR);
                    TSqlFormattingState innerState = new TSqlFormattingState(state);
                    ProcessSqlNodeList(contentElement.SelectNodes("*"), innerState);
                    //if there was a linebreak in the parens content, or if it wanted one to follow, then put linebreaks before and after.
                    if (innerState.BreakExpected || innerState.OutputContainsLineBreak)
                    {
                        state.WhiteSpace_BreakToNextLine();
                        state.Assimilate(innerState);
                        state.WhiteSpace_BreakToNextLine();
                    }
                    else
                    {
                        state.Assimilate(innerState);
                    }
                    state.AddOutputContent(FormatOperator(")"), Interfaces.SqlHtmlConstants.CLASS_OPERATOR);
                    if (contentElement.Name.Equals(SqlXmlConstants.ENAME_EXPRESSION_PARENS))
                        state.DecrementIndent();
                    state.WordSeparatorExpected = true;
                    break;

                case SqlXmlConstants.ENAME_BEGIN_END_BLOCK:
                case SqlXmlConstants.ENAME_TRY_BLOCK:
                case SqlXmlConstants.ENAME_CATCH_BLOCK:
                    if (contentElement.ParentNode.Name.Equals(SqlXmlConstants.ENAME_SQL_CLAUSE)
                        && contentElement.ParentNode.ParentNode.Name.Equals(SqlXmlConstants.ENAME_SQL_STATEMENT)
                        && contentElement.ParentNode.ParentNode.ParentNode.Name.Equals(SqlXmlConstants.ENAME_CONTAINER_SINGLESTATEMENT)
                        )
                        state.DecrementIndent();
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CONTAINER_OPEN), state);
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CONTAINER_MULTISTATEMENT), state);
                    state.DecrementIndent();
                    state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CONTAINER_CLOSE), state);
                    state.IncrementIndent();
                    if (contentElement.ParentNode.Name.Equals(SqlXmlConstants.ENAME_SQL_CLAUSE)
                        && contentElement.ParentNode.ParentNode.Name.Equals(SqlXmlConstants.ENAME_SQL_STATEMENT)
                        && contentElement.ParentNode.ParentNode.ParentNode.Name.Equals(SqlXmlConstants.ENAME_CONTAINER_SINGLESTATEMENT)
                        )
                        state.IncrementIndent();
                    break;

                case SqlXmlConstants.ENAME_CASE_STATEMENT:
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CONTAINER_OPEN), state);
                    state.IncrementIndent();
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CASE_INPUT), state);
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CASE_WHEN), state);
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CASE_ELSE), state);
                    if (ExpandCaseStatements)
                        state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CONTAINER_CLOSE), state);
                    state.DecrementIndent();
                    break;

                case SqlXmlConstants.ENAME_CASE_WHEN:
                case SqlXmlConstants.ENAME_CASE_THEN:
                case SqlXmlConstants.ENAME_CASE_ELSE:
                    if (ExpandCaseStatements)
                        state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CONTAINER_OPEN), state);
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CONTAINER_GENERALCONTENT), state.IncrementIndent());
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_CASE_THEN), state);
                    state.DecrementIndent();
                    break;

                case SqlXmlConstants.ENAME_AND_OPERATOR:
                case SqlXmlConstants.ENAME_OR_OPERATOR:
                    if (ExpandBooleanExpressions)
                        state.BreakExpected = true;
                    ProcessSqlNodeList(contentElement.SelectNodes("*"), state);
                    break;

                case SqlXmlConstants.ENAME_COMMENT_MULTILINE:
                    WhiteSpace_SeparateComment(contentElement, state);
                    state.AddOutputContent("/*" + contentElement.InnerText + "*/", Interfaces.SqlHtmlConstants.CLASS_COMMENT);
                    if (contentElement.ParentNode.Name.Equals(SqlXmlConstants.ENAME_SQL_STATEMENT))
                        state.BreakExpected = true;
                    else
                    {
                        state.WordSeparatorExpected = true;
                    }
                    break;

                case SqlXmlConstants.ENAME_COMMENT_SINGLELINE:
                    WhiteSpace_SeparateComment(contentElement, state);
                    state.AddOutputContent("--" + contentElement.InnerText.Replace("\r", "").Replace("\n", ""), Interfaces.SqlHtmlConstants.CLASS_COMMENT);
                    state.BreakExpected = true;
                    state.SourceBreakPending = true;
                    break;

                case SqlXmlConstants.ENAME_STRING:
                case SqlXmlConstants.ENAME_NSTRING:
                    WhiteSpace_SeparateWords(state);
                    string outValue = null;
                    if (contentElement.Name.Equals(SqlXmlConstants.ENAME_NSTRING))
                        outValue = "N'" + contentElement.InnerText.Replace("'", "''") + "'";
                    else
                        outValue = "'" + contentElement.InnerText.Replace("'", "''") + "'";
                    state.AddOutputContent(outValue, Interfaces.SqlHtmlConstants.CLASS_STRING);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlXmlConstants.ENAME_BRACKET_QUOTED_NAME:
                    WhiteSpace_SeparateWords(state);
                    state.AddOutputContent("[" + contentElement.InnerText.Replace("]", "]]") + "]");
                    state.WordSeparatorExpected = true;
                    break;

                case SqlXmlConstants.ENAME_QUOTED_STRING:
                    WhiteSpace_SeparateWords(state);
                    state.AddOutputContent("\"" + contentElement.InnerText.Replace("\"", "\"\"") + "\"");
                    state.WordSeparatorExpected = true;
                    break;

                case SqlXmlConstants.ENAME_COMMA:
                    //comma always ignores requested word spacing
                    if (TrailingCommas)
                    {
                        state.AddOutputContent(FormatOperator(","), Interfaces.SqlHtmlConstants.CLASS_OPERATOR);

                        if (ExpandCommaLists
                            && !(contentElement.ParentNode.Name.Equals(SqlXmlConstants.ENAME_DDLDETAIL_PARENS)
                                || contentElement.ParentNode.Name.Equals(SqlXmlConstants.ENAME_FUNCTION_PARENS)
                                )
                            )
                            state.BreakExpected = true;
                        else
                            state.WordSeparatorExpected = true;
                    }
                    else
                    {
                        if (ExpandCommaLists
                            && !(contentElement.ParentNode.Name.Equals(SqlXmlConstants.ENAME_DDLDETAIL_PARENS)
                                || contentElement.ParentNode.Name.Equals(SqlXmlConstants.ENAME_FUNCTION_PARENS)
                                )
                            )
                        {
                            state.WhiteSpace_BreakToNextLine();
                            state.AddOutputContent(FormatOperator(","), Interfaces.SqlHtmlConstants.CLASS_OPERATOR);
                            if (SpaceAfterExpandedComma)
                                state.WordSeparatorExpected = true;
                        }
                        else
                        {
                            WhiteSpace_BreakAsExpected(state);
                            state.AddOutputContent(FormatOperator(","), Interfaces.SqlHtmlConstants.CLASS_OPERATOR);
                            state.WordSeparatorExpected = true;
                        }

                    }
                    break;

                case SqlXmlConstants.ENAME_PERIOD:
                case SqlXmlConstants.ENAME_SEMICOLON:
                case SqlXmlConstants.ENAME_SCOPERESOLUTIONOPERATOR:
                    //always ignores requested word spacing, and doesn't request a following space either.
                    state.WordSeparatorExpected = false;
                    WhiteSpace_BreakAsExpected(state);
                    state.AddOutputContent(FormatOperator(contentElement.InnerText), Interfaces.SqlHtmlConstants.CLASS_OPERATOR);
                    break;

                case SqlXmlConstants.ENAME_ASTERISK:
                case SqlXmlConstants.ENAME_OTHEROPERATOR:
                    WhiteSpace_SeparateWords(state);
                    state.AddOutputContent(FormatOperator(contentElement.InnerText), Interfaces.SqlHtmlConstants.CLASS_OPERATOR);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlXmlConstants.ENAME_COMPOUNDKEYWORD:
                    WhiteSpace_SeparateWords(state);
                    state.SetRecentKeyword(contentElement.Attributes[SqlXmlConstants.ANAME_SIMPLETEXT].Value);
                    state.AddOutputContent(FormatKeyword(contentElement.Attributes[SqlXmlConstants.ANAME_SIMPLETEXT].Value), Interfaces.SqlHtmlConstants.CLASS_KEYWORD);
                    state.WordSeparatorExpected = true;
                    ProcessSqlNodeList(contentElement.SelectNodes(SqlXmlConstants.ENAME_COMMENT_MULTILINE + " | " + SqlXmlConstants.ENAME_COMMENT_SINGLELINE), state.IncrementIndent());
                    state.DecrementIndent();
                    state.WordSeparatorExpected = true;
                    break;

                case SqlXmlConstants.ENAME_OTHERKEYWORD:
                case SqlXmlConstants.ENAME_DATATYPE_KEYWORD:
                    WhiteSpace_SeparateWords(state);
                    state.SetRecentKeyword(contentElement.InnerText);
                    state.AddOutputContent(FormatKeyword(contentElement.InnerText), Interfaces.SqlHtmlConstants.CLASS_KEYWORD);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlXmlConstants.ENAME_PSEUDONAME:
                    WhiteSpace_SeparateWords(state);
                    state.AddOutputContent(FormatKeyword(contentElement.InnerText), Interfaces.SqlHtmlConstants.CLASS_KEYWORD);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlXmlConstants.ENAME_FUNCTION_KEYWORD:
                    WhiteSpace_SeparateWords(state);
                    state.SetRecentKeyword(contentElement.InnerText);
                    state.AddOutputContent(contentElement.InnerText, Interfaces.SqlHtmlConstants.CLASS_FUNCTION);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlXmlConstants.ENAME_OTHERNODE:
                case SqlXmlConstants.ENAME_NUMBER_VALUE:
                case SqlXmlConstants.ENAME_MONETARY_VALUE:
                case SqlXmlConstants.ENAME_BINARY_VALUE:
                case SqlXmlConstants.ENAME_LABEL:
                    WhiteSpace_SeparateWords(state);
                    state.AddOutputContent(contentElement.InnerText);
                    state.WordSeparatorExpected = true;
                    break;

                case SqlXmlConstants.ENAME_WHITESPACE:
                    //take note if it's a line-breaking space, but don't DO anything here
                    if (Regex.IsMatch(contentElement.InnerText, @"(\r|\n)+"))
                        state.SourceBreakPending = true;
                    break;
                default:
                    throw new Exception("Unrecognized element in SQL Xml!");
            }

            if (initialIndent != state.IndentLevel)
                throw new Exception("Messed up the indenting!! Check code/stack or panic!");
        }

        private string FormatKeyword(string keyword)
        {
            string outputKeyword;
            if (!KeywordMapping.TryGetValue(keyword, out outputKeyword))
                outputKeyword = keyword;

            if (UppercaseKeywords)
                return outputKeyword.ToUpper(System.Globalization.CultureInfo.InvariantCulture);
            else
                return outputKeyword.ToLower();
        }

        private string FormatOperator(string operatorValue)
        {
            if (UppercaseKeywords)
                return operatorValue.ToUpper(System.Globalization.CultureInfo.InvariantCulture);
            else
                return operatorValue.ToLower();
        }

        private void WhiteSpace_SeparateStatements(XmlElement contentElement, TSqlFormattingState state)
        {
            if (state.StatementBreakExpected)
            {
                //check whether this is a DECLARE/SET clause with similar precedent, and therefore exempt from double-linebreak.
                XmlElement thisClauseStarter = FirstSemanticElementChild(contentElement);
                if (!(thisClauseStarter != null
                    && thisClauseStarter.Name.Equals(SqlXmlConstants.ENAME_OTHERKEYWORD)
                    && state.GetRecentKeyword() != null
                    && ((thisClauseStarter.InnerXml.ToUpper(System.Globalization.CultureInfo.InvariantCulture).Equals("SET")
                            && state.GetRecentKeyword().Equals("SET")
                            )
                        || (thisClauseStarter.InnerXml.ToUpper(System.Globalization.CultureInfo.InvariantCulture).Equals("DECLARE")
                            && state.GetRecentKeyword().Equals("DECLARE")
                            )
                        || (thisClauseStarter.InnerXml.ToUpper(System.Globalization.CultureInfo.InvariantCulture).Equals("PRINT")
                            && state.GetRecentKeyword().Equals("PRINT")
                            )
                        )
                    ))
                    state.AddOutputLineBreak();

                state.AddOutputLineBreak();
                state.Indent(state.IndentLevel);
                state.BreakExpected = false;
                state.SourceBreakPending = false;
                state.StatementBreakExpected = false;
                state.WordSeparatorExpected = false;
            }
        }

        private XmlElement FirstSemanticElementChild(XmlElement contentElement)
        {
            XmlElement target = null;
            while (contentElement != null)
            {
                target = (XmlElement)contentElement.SelectSingleNode(string.Format("*[local-name() != '{0}' and local-name() != '{1}' and local-name() != '{2}']",
                    SqlXmlConstants.ENAME_WHITESPACE,
                    SqlXmlConstants.ENAME_COMMENT_MULTILINE,
                    SqlXmlConstants.ENAME_COMMENT_SINGLELINE));

                if (target != null
                    && (target.Name.Equals(SqlXmlConstants.ENAME_SQL_CLAUSE)
                        || target.Name.Equals(SqlXmlConstants.ENAME_DDL_PROCEDURAL_BLOCK)
                        || target.Name.Equals(SqlXmlConstants.ENAME_DDL_OTHER_BLOCK)
                        )
                    )
                    contentElement = target;
                else
                    contentElement = null;
            }

            return target;
        }

        private void WhiteSpace_SeparateWords(TSqlFormattingState state)
        {
            if (state.BreakExpected || state.AdditionalBreakExpected)
            {
                bool wasUnIndent = state.UnIndentInitialBreak;
                if (wasUnIndent) state.DecrementIndent();
                WhiteSpace_BreakAsExpected(state);
                if (wasUnIndent) state.IncrementIndent();
            }
            else if (state.WordSeparatorExpected)
            {
                state.AddOutputSpace();
            }
            state.UnIndentInitialBreak = false;
            state.SourceBreakPending = false;
            state.WordSeparatorExpected = false;
        }

        private void WhiteSpace_SeparateComment(XmlElement contentElement, TSqlFormattingState state)
        {
            if ((state.BreakExpected || state.WordSeparatorExpected) && state.SourceBreakPending)
            {
                state.BreakExpected = true;
                WhiteSpace_BreakAsExpected(state);
            }
            else if (state.WordSeparatorExpected)
                state.AddOutputSpace();
            state.SourceBreakPending = false;
            state.WordSeparatorExpected = false;
        }

        private void WhiteSpace_BreakAsExpected(TSqlFormattingState state)
        {
            if (state.BreakExpected)
                state.WhiteSpace_BreakToNextLine();
            if (state.AdditionalBreakExpected)
            {
                state.WhiteSpace_BreakToNextLine();
                state.AdditionalBreakExpected = false;
            }
        }

        class TSqlFormattingState : BaseFormatterState
        {
            //normal constructor
            public TSqlFormattingState(bool htmlOutput, string indentString, int spacesPerTab, int maxLineWidth, int initialIndentLevel)
                : base(htmlOutput)
            {
                IndentLevel = initialIndentLevel;
                HtmlOutput = htmlOutput;
                IndentString = indentString;
                MaxLineWidth = maxLineWidth;

                int tabCount = indentString.Split('\t').Length - 1;
                int tabExtraCharacters = tabCount * (spacesPerTab - 1);
                IndentLength = indentString.Length + tabExtraCharacters;
            }

            //special "we want isolated state, but inheriting existing conditions" constructor
            public TSqlFormattingState(TSqlFormattingState sourceState)
                : base(sourceState.HtmlOutput)
            {
                IndentLevel = sourceState.IndentLevel;
                HtmlOutput = sourceState.HtmlOutput;
                IndentString = sourceState.IndentString;
                IndentLength = sourceState.IndentLength;
                MaxLineWidth = sourceState.MaxLineWidth;
                //TODO: find a way out of the cross-dependent wrapping maze...
                //CurrentLineLength = sourceState.CurrentLineLength;
                CurrentLineLength = IndentLevel * IndentLength;
                CurrentLineHasContent = sourceState.CurrentLineHasContent;
            }

            private string IndentString { get; set; }
            private int IndentLength { get; set; }
            private int MaxLineWidth { get; set; }

            public bool StatementBreakExpected { get; set; }
            public bool BreakExpected { get; set; }
            public bool WordSeparatorExpected { get; set; }
            public bool SourceBreakPending { get; set; }
            public bool AdditionalBreakExpected { get; set; }

            public bool UnIndentInitialBreak { get; set; }
            public int IndentLevel { get; private set; }
            public int CurrentLineLength { get; private set; }
            public bool CurrentLineHasContent { get; private set; }

            public override void AddOutputContent(string content)
            {
                AddOutputContent(content, null);
            }

            public override void AddOutputContent(string content, string htmlClassName)
            {
                if (CurrentLineHasContent && (content.Length + CurrentLineLength > MaxLineWidth))
                    WhiteSpace_BreakToNextLine();

                base.AddOutputContent(content, htmlClassName);

                CurrentLineHasContent = true;
                CurrentLineLength += content.Length;
            }

            public override void AddOutputLineBreak()
            {
#if DEBUG
                //hints for debugging line-width issues:
                //_outBuilder.Append(" (" + CurrentLineLength.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")");
#endif

                //if linebreaks are added directly in the content (eg in comments or strings), they 
                // won't be accounted for here - that's ok.
                base.AddOutputLineBreak();
                CurrentLineLength = 0;
                CurrentLineHasContent = false;
            }

            internal void AddOutputSpace()
            {
                _outBuilder.Append(" ");
            }

            public void Indent(int indentLevel)
            {
                for (int i = 0; i < indentLevel; i++)
                {
                    _outBuilder.Append(IndentString);
                    CurrentLineLength += IndentLength;
                }
            }

            internal void WhiteSpace_BreakToNextLine()
            {
                AddOutputLineBreak();
                Indent(IndentLevel);
                BreakExpected = false;
                SourceBreakPending = false;
                WordSeparatorExpected = false;
            }

            //for linebreak detection, use actual string content rather than counting "AddOutputLineBreak()" calls,
            // because we also want to detect the content of strings and comments.
            private static Regex _lineBreakMatcher = new Regex(@"(\r|\n)+", RegexOptions.Compiled);
            public bool OutputContainsLineBreak { get { return _lineBreakMatcher.IsMatch(_outBuilder.ToString()); } }

            public void Assimilate(TSqlFormattingState partialState)
            {
                //TODO: find a way out of the cross-dependent wrapping maze...
                CurrentLineLength = CurrentLineLength + partialState.CurrentLineLength;
                CurrentLineHasContent = CurrentLineHasContent || partialState.CurrentLineHasContent;
                _outBuilder.Append(partialState.DumpOutput());
            }


            private Dictionary<int, string> _mostRecentKeywordsAtEachLevel = new Dictionary<int, string>();

            public TSqlFormattingState IncrementIndent()
            {
                IndentLevel++;
                return this;
            }

            public TSqlFormattingState DecrementIndent()
            {
                IndentLevel--;
                return this;
            }

            public void SetRecentKeyword(string ElementName)
            {
                if (!_mostRecentKeywordsAtEachLevel.ContainsKey(IndentLevel))
                    _mostRecentKeywordsAtEachLevel.Add(IndentLevel, ElementName.ToUpper(System.Globalization.CultureInfo.InvariantCulture));
            }

            public string GetRecentKeyword()
            {
                string keywordFound = null;
                int? keywordFoundAt = null;
                foreach (int key in _mostRecentKeywordsAtEachLevel.Keys)
                {
                    if ((keywordFoundAt == null || keywordFoundAt.Value > key) && key >= IndentLevel)
                    {
                        keywordFoundAt = key;
                        keywordFound = _mostRecentKeywordsAtEachLevel[key];
                    }
                }
                return keywordFound;
            }

            public void ResetKeywords()
            {
                List<int> descendentLevelKeys = new List<int>();
                foreach (int key in _mostRecentKeywordsAtEachLevel.Keys)
                    if (key >= IndentLevel)
                        descendentLevelKeys.Add(key);
                foreach (int key in descendentLevelKeys)
                    _mostRecentKeywordsAtEachLevel.Remove(key);
            }
        }
    }
}
