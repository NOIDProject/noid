﻿// Copyright © 2016-2017 NoID Developers. All rights reserved.
// Distributed under the MIT software license, see the accompanying
// file COPYING or http://www.opensource.org/licenses/mit-license.php.

using System;
using System.Web;
using System.IO;
using System.Web.Configuration;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using NoID.FHIR.Profile;
using NoID.Utilities;

namespace NoID.Network.Services
{
    //Workflow
    //1) Save message in Spark FHIR for persisting.
    //2) If biometic probe message (FHIR Media type), route to biometic match engine router
    //3) If patient message (FHIR patient type), add the patient message and expire any previous records. 
    //      all NoID patient resources must have a session id, local healthcare node id and hub id

    public class FHIRMessageRouter
    {
        private PatientFHIRProfile _patientFHIRProfile;
        private string _responseText;
        private Patient _patient = null;
        private Media _biometics = null;
        private Uri _sparkEndpoint = new Uri(WebConfigurationManager.AppSettings["SparkEndpointAddress"]);
        private Exception _exception;

        public FHIRMessageRouter(HttpContext context)
        {
            try
            {
                Resource newResource = FHIRUtilities.StreamToFHIR(new StreamReader(context.Request.InputStream));

                switch (newResource.TypeName.ToLower())
                {
                    case "patient":
                        _patient = (Patient)newResource;
                        //TODO check for existing patient and expire old messages for the patient.
                        if (!(SendPatientToSparkServer()))
                        {
                            _responseText = "Error sending Patient FHIR message to the Spark FHIR endpoint. " + ExceptionString;
                        }
                        break;
                    case "media":
                        _biometics = (Media)newResource;
                        // TODO send to biometric match engine. If found, add patient reference to FHIR message.
                        if (!(SendBiometicsToSparkServer()))
                        {
                            _responseText = "Error sending Biometric Media FHIR message to the Spark FHIR endpoint. " + ExceptionString;
                        }
                        break;
                    default:
                        _responseText = newResource.TypeName.ToLower() + " not supported.";
                        break;
                }
            }
            catch (Exception ex)
            {
                _responseText = "Error processing FHIR message: " + ex.Message;
            }
        }

        public bool SendPatientToSparkServer()
        {
            FhirClient client = new FhirClient(_sparkEndpoint);

            if (!(Patient == null))
            {
                try
                {
                    Resource response = client.Create(Patient);
                    _responseText = FHIRUtilities.FHIRToString(response);
                }
                catch (Exception ex)
                {
                    _responseText = ex.Message;
                    _exception = ex;
                    return false;
                }
            }
            return true;
        }

        public bool SendBiometicsToSparkServer()
        {
            FhirClient client = new FhirClient(_sparkEndpoint);

            if (!(Patient == null))
            {
                try
                {
                    Resource response = client.Create(Biometics);
                    _responseText = FHIRUtilities.FHIRToString(response);
                }
                catch (Exception ex)
                {
                    _responseText = ex.Message;
                    _exception = ex;
                    return false;
                }
            }
            return true;
        }

        public PatientFHIRProfile PatientFHIRProfile
        {
            get { return _patientFHIRProfile; }
            private set { _patientFHIRProfile = value; }
        }

        public Uri SparkEndpoint
        {
            get { return _sparkEndpoint; }
            private set { _sparkEndpoint = value; }
        }

        public Patient Patient
        {
            get { return _patient; }
            private set { _patient = value; }
        }

        public Media Biometics
        {
            get { return _biometics; }
            private set { _biometics = value; }
        }

        public string ResponseText
        {
            get { return _responseText; }
        }

        public string ExceptionString
        {
            get
            {
                if (!(_exception == null))
                {
                    return _exception.Message;
                }
                else
                {
                    return "";
                }
            }
        }
    }
}