# **PRTGAmpel Service - Documentation**

## **Description**  
The **PRTGAmpel** service monitors sensor data from multiple servers provided by the `PRTGService` and controls a USB traffic light to visually represent the server statuses. The traffic light displays the current server state (`OK`, `Warning`, `Error`) and supports animation modes for specific events.  
**Supported Traffic Light**: Cleware USB-TischAmpel4

---

## **Prerequisites**  
- **Operating System**: Windows 10/11 or Server editions with .NET support  
- **.NET Framework**: .NET Framework 4.8.1  
- **USB Device**: Cleware USB-TischAmpel4 
- **Administrator Rights**: Required for installation and uninstallation  

---

## **Installation Instructions**  
**Download the latest version under Releases**
### **Steps**  
1. **Prepare Files**  
   - Extract the contents of `PRTGAmpel.zip`.  

2. **Run the Installation Script**  
   - Execute `install.bat` as an administrator.  
   - The script performs the following tasks:  
     - Creates necessary directories:  
       - `C:\ProgramData\PRTGAmpel\Program`  
       - `C:\ProgramData\PRTGAmpel\Logs`  
     - Copies the program files.  
     - Launches the **Configurator** to generate `config.json` based on `appsettings.json`.  
     - Installs and starts the `PRTGAmpel` service automatically.  

3. **Verify Configuration**  
   - The configuration file `config.json` is located at `C:\ProgramData\PRTGAmpel\Appsettings\`.  
   - Ensure server information is correctly imported from `appsettings.json`.  

---

## **Uninstallation Instructions**  

### **Steps**  
1. **Stop and Remove the Service**  
   - Execute `uninstall.bat` as an administrator.  
   - The script performs the following tasks:  
     - Stops the `PRTGAmpel` service if active.  
     - Removes the service from the Windows Services Manager.  
     - Deletes the installation directory (`C:\ProgramData\PRTGAmpel\Program`) and associated logs.  

---

## **Configuration**  

### **Files**  
- **`appsettings.json`**  
  - Used by `Configurator.exe` to generate `config.json`.  
  - Contains server details (`ServerIP`, username, password, protocol, etc.).  
  - Path: `C:\ProgramData\PRTGSensorStatus\Appsettings\`.  

- **`config.json`**  
  - Read by the `PRTGAmpel` service to map server addresses.  
  - Example structure:  
    ```json  
    {  
      "ServerIPs": [  
        "server1.domain.de",  
        "server2.domain.de"  
      ]  
    }  
    ```  

---

## **Log Files**  
- **Location**:  
  - `C:\ProgramData\PRTGAmpel\Logs\ServiceLog.txt`  
- **Contents**:  
  - Details about the service's operations, such as:  
    - Successful processing of sensor data.  
    - Changes to the traffic light's state.  
    - Errors or warnings in controlling the traffic light.  

---

## **Troubleshooting**  

### **Common Issues**  
1. **Service Does Not Start**  
   - Ensure you executed `install.bat` as an administrator.  
   - Check logs located at `C:\ProgramData\PRTGAmpel\Logs`.  

2. **Traffic Light Is Unresponsive**  
   - Verify that the USB traffic light is properly connected.  
   - Ensure the `config.json` configuration is correct.  

3. **Missing Logs**  
   - Ensure the user who installed the service has write permissions for the logs folder.  

4. **Configuration Issues**  
   - Ensure `appsettings.json` is complete before running the **Configurator**.  

---

## **Technical Details**  

- **Configurator (`Configurator.exe`)**  
  - Reads `appsettings.json` from `PRTGSensorStatus`.  
  - Generates `config.json` for the `PRTGAmpel` service.  

- **Service Entries**  
  - The service is registered under the name `PRTGAmpel` in the Windows Services Manager.  
  - Configured to start automatically upon installation.  

(C) Tom Kölsch @ CNAG
