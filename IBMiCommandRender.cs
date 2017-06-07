﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;

namespace IBMiCmd
{
    class IBMiCommandRender
    {
        internal static string[] RenderRelicRebuildScript(string tmp)
        {
            string buildDir = IBMi.GetConfig("relicdir");
            if (!buildDir.EndsWith("/"))
            {
                buildDir += '/';
            }

            return new string[] {
                $"QUOTE RCMD CD '{ buildDir }'",
                $"QUOTE RCMD RBLD { IBMi.GetConfig("reliclib") }",
                "ASCII",
                $"RECV { buildDir }RELICBLD.log \"{ tmp }\""
            };
        }

        internal static string[] RenderFFDCollectionScript(List<SourceLine> src, string[] tmp)
        {
            IBMiUtilities.DebugLog("RenderFFDCollectionScript");
            string[] cmd = new string[(src.Count * 3) + 2];
            int i = 0, t = 0;
            // Run commands on remote
            cmd[i++] = "ASCII";
            cmd[i++] = $"QUOTE RCMD CHGLIBL LIBL({ IBMi.GetConfig("datalibl").Replace(',', ' ')})  CURLIB({ IBMi.GetConfig("curlib") })";
            foreach (SourceLine sl in src)
            {
                cmd[i++] = $"QUOTE RCMD {IBMi.GetConfig("installlib")}/NPPDSPFFD {sl.searchResult}";
                cmd[i++] = $"RECV /home/{ IBMi.GetConfig("username") }/{ sl.searchResult }.tmp \"{ tmp[t++] }\"";
                cmd[i++] = $"QUOTE RCMD RMVLNK OBJLNK('/home/{ IBMi.GetConfig("username") }/{ sl.searchResult }.tmp')";
            }
            
            IBMiUtilities.DebugLog("RenderFFDCollectionScript - DONE!");
            return cmd;
        }

        internal static string[] RenderCommandDescriptionCollection(string command)
        {
            IBMiUtilities.DebugLog("RenderCommandDescriptionCollection");
            string[] cmd = new string[5];
            // Run commands on remote
            int i = 0;
            cmd[i] = "ASCII";
            cmd[++i] = $"QUOTE RCMD CHGLIBL LIBL({ IBMi.GetConfig("datalibl").Replace(',', ' ')})  CURLIB({ IBMi.GetConfig("curlib") })";
            cmd[++i] = $"QUOTE RCMD { IBMi.GetConfig("installlib") }/NPPRTVCMD {command}";
            cmd[++i] = $"RECV /home/{ IBMi.GetConfig("username") }/{ command }.cdml { Main.FileCacheDirectory }{ command }.cdml";
            cmd[++i] = $"QUOTE RCMD RMVLNK OBJLNK('/home/{ IBMi.GetConfig("username") }/{ sl.searchResult }.tmp')";

            IBMiUtilities.DebugLog("RenderCommandDescriptionCollection - DONE!");
            return cmd;
        }

        private static void UpdateFileCache(string file)
        {
            XmlSerializer xf = new XmlSerializer(typeof(DataStructure));
            string cacheFile = $"{Main.FileCacheDirectory}{d.name.TrimEnd()}.ffd";
            using (Stream stream = File.Open(cacheFile, FileMode.Create))
            {
                xf.Serialize(stream, d);
            }
        }

        internal static string[] RenderRemoteInstallScript(List<string> sourceFiles, string library)
        {
            // Make room for <upload, copy, delete, compile> for each file
            string[] cmd = new string[sourceFiles.Count * 4 + 4];
            int i = 0;
            cmd[i++] = "ASCII";
            cmd[i++] = "QUOTE RCMD CRTPF FILE(QTEMP/NPPCLSRC)  RCDLEN(112) FILETYPE(*SRC) MAXMBRS(*NOMAX) TEXT('Deploy NPP plugin commands')";
            cmd[i++] = "QUOTE RCMD CRTPF FILE(QTEMP/NPPCMDSRC) RCDLEN(112) FILETYPE(*SRC) MAXMBRS(*NOMAX) TEXT('Deploy NPP plugin commands')";
            cmd[i++] = "QUOTE RCMD CRTPF FILE(QTEMP/NPPRPGSRC) RCDLEN(240) FILETYPE(*SRC) MAXMBRS(*NOMAX) TEXT('Deploy NPP plugin commands')";
            foreach (string file in sourceFiles)
            {
                string fileName = file.Substring(file.LastIndexOf("\\") + 1);
                string member = fileName.Substring(fileName.LastIndexOf("-") + 1, fileName.LastIndexOf(".") - (fileName.LastIndexOf("-") + 1));
                string sourceFile = null, crtCmd = null;

                switch (fileName.Substring(fileName.Length - 4))
                {
                    case ".clp":
                        sourceFile = "NPPCLSRC";
                        crtCmd = $"CRTCLPGM PGM({library}/{member}) SRCFILE(QTEMP/NPPCLSRC) SRCMBR({member}) REPLACE(*YES) TEXT('{Main.PluginDescription}')";
                        break;
                    case ".cmd":
                        sourceFile = "NPPCMDSRC";
                        crtCmd = $"CRTCMD CMD({library}/{member}) PGM({library}/{member}) SRCFILE(QTEMP/NPPCMDSRC) SRCMBR({member}) REPLACE(*YES) TEXT('{Main.PluginDescription}')";
                        break;
                    case ".rpgle":
                        sourceFile = "NPPRPGSRC";
                        crtCmd = $"CRTBNDRPG PGM({library}/{member}) SRCFILE(QTEMP/{sourceFile}) SRCMBR({member}) REPLACE(*YES) TEXT('{Main.PluginDescription}')";
                        break;
                    default:
                        continue;
                }

                cmd[i++] = $"SEND { file } /home/{ IBMi.GetConfig("username") }/{ fileName }";
                cmd[i++] = $"QUOTE RCMD CPYFRMSTMF FROMSTMF('/home/{ IBMi.GetConfig("username") }/{ fileName }') TOMBR('/QSYS.LIB/QTEMP.LIB/{ sourceFile }.FILE/{ member }.MBR')";
                cmd[i++] = $"QUOTE RCMD RMVLNK OBJLNK('/home/{ IBMi.GetConfig("username") }/{ fileName }')";
                cmd[i++] = $"QUOTE RCMD { crtCmd }";
            }
            return cmd;
        }
    }
}
