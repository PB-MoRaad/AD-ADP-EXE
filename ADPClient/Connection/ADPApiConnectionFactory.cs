/*# This file is part of adp-api-library.
# https://github.com/adplabs/adp-connection-NET

# Copyright © 2015-2016 ADP, LLC.

# Licensed under the Apache License, Version 2.0 (the “License”);
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at

# http://www.apache.org/licenses/LICENSE-2.0

# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an “AS IS” BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either
# express or implied.  See the License for the specific language
# governing permissions and limitations under the License.
*/
using System;
using System.IO;

namespace ADPClient
{

    /// <summary>
    /// 
    /// </summary>
    public class ADPApiConnectionFactory
    {

        /// <summary>
        /// 
        /// </summary>
        public ADPApiConnectionFactory()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        public static ADPApiConnection InitConnection()
        {
            string configFileName = "development.json";

            //StreamReader sr = new StreamReader("..\\..\\Content\\config\\" + configFileName);
            StreamReader sr = new StreamReader("Content\\config\\" + configFileName);

            string clientconfig = sr.ReadToEnd();

            //string clientconfig = ADPClientDemo.Properties.Settings.Default.ClientCredentialConfiguration;
            //ADPAccessToken token = null;

            if (String.IsNullOrEmpty(clientconfig))
            {
                Console.WriteLine("Settings file or default options not available.");
                return null;
            }
            else
            {
                ClientCredentialConfiguration connectionCfg = JSONUtil.Deserialize<ClientCredentialConfiguration>(clientconfig);
                ClientCredentialConnection connection = (ClientCredentialConnection)ADPApiConnectionFactory.createConnection(connectionCfg);
                return connection;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionCfg"></param>
        /// <returns></returns>
        public static ADPApiConnection createConnection(ConnectionConfiguration connectionCfg)
        {

            if (connectionCfg is AuthorizationCodeConfiguration)
            {
                return new AuthorizationCodeConnection(connectionCfg);
            }
            else if (connectionCfg is ClientCredentialConfiguration)
            {
                return new ClientCredentialConnection(connectionCfg);
            }
            else
            {
                throw new Exception("Grant type / Configuration type not implemented.");
            }
        }
    }
}

