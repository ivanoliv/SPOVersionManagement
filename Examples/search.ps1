#############################################################################

#                                                                                                                           #

#   This Sample Code is provided for the purpose of illustration only       #

#   and is not intended to be used in a production environment.  THIS       #

#   SAMPLE CODE AND ANY RELATED INFORMATION ARE PROVIDED "AS IS" WITHOUT    #

#   WARRANTY OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT    #

#   LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS     #

#   FOR A PARTICULAR PURPOSE.  We grant You a nonexclusive, royalty-free    #

#   right to use and modify the Sample Code and to reproduce and distribute #

#   the object code form of the Sample Code, provided that You agree:       #

#   (i) to not use Our name, logo, or trademarks to market Your software    #

#   product in which the Sample Code is embedded; (ii) to include a valid   #

#   copyright notice on Your software product in which the Sample Code is   #

#   embedded; and (iii) to indemnify, hold harmless, and defend Us and      #

#   Our suppliers from and against any claims or lawsuits, including        #

#   attorneys' fees, that arise or result from the use or distribution      #

#   of the Sample Code.                                                     #

# ###########################################################################

#                                                                           #

#  Add and Modify Search Topology SAMPLE WRITTEN BY SCOTT STEWART           #

#  (Support for SP2016 Beta now included)                                   #

$version = "V2_8_2"

#############################################################################

# A Special thanks to Neil Hodgkinson for his endless sharing of            #

#  his Search knowledge                                                     #

# Thanks to Brian Pendergrass for his input on understanding how components #

#  flow as well as storage locations.                                       #

#############################################################################

# Credits:                                                                  #

#  Koos Botha for his code to assist with the Form Creation                 #

#  Joe Rodgers for his code for the Start Service Instances do while loops  #

#   and Initialize Admin component - blog http://blogs.msdn.com/b/josrod/   #

#############################################################################

 

 

$ErrorActionPreference = "SilentlyContinue"

$Global:isNewSA                = $false

$Global:isIndexLocationchanged = $false

 

#region Static Definitions

#DEFINE COMPONENT TYPES

$Admin     = "AdminComponent"

$Crawl     = "CrawlComponent"

$Content   = "ContentProcessingComponent"

$Index     = "IndexComponent"

$Query     = "QueryProcessingComponent"

$Analytics = "AnalyticsProcessingComponent"

 

#DEFINE MESSAGE TYPES

$errMessage    = 1

$InfoMessage   = 2

$StatusMessage = 3

 

#endregion Static Definitions

 

 

#Loading Assemblies

#region Import the Assemblies

    Write-Host "Loading the SharePoint Snapin, Forms and Drawing assemblies" -BackgroundColor DarkYellow

    Add-PSSnapin microsoft.sharepoint.powershell -ErrorAction $ErrorActionPreference

    [reflection.assembly]::LoadWithPartialName( "System.Windows.Forms") | Out-Null

    [reflection.assembly]::LoadWithPartialName( "System.Drawing") | out-null

#endregion Import Assemblies

 

#Here we create all objects form my the form

#region Generated Form Objects

$FormSearch                          = New-Object System.Windows.Forms.Form

$buttonCreateNew     = New-Object System.Windows.Forms.Button

$buttonImport        = New-Object System.Windows.Forms.Button

$buttonChange        = New-Object System.Windows.Forms.Button

$buttonUpdate        = New-Object System.Windows.Forms.Button

$buttonExit          = New-Object System.Windows.Forms.Button

$ddlSSA              = New-Object System.Windows.Forms.ComboBox

 

$gridAdmin           = New-Object System.Windows.Forms.ListBox

$gridCrawl           = New-Object System.Windows.Forms.ListBox

$gridContent         = New-Object System.Windows.Forms.ListBox

$gridIndex           = New-Object System.Windows.Forms.ListBox

$gridQuery           = New-Object System.Windows.Forms.ListBox

$gridAnalytics       = New-Object System.Windows.Forms.ListBox

 

$lblHeader           = New-Object System.Windows.Forms.Label

$lblSSA              = New-Object System.Windows.Forms.Label

$lblHeader2          = New-Object System.Windows.Forms.Label

$lblHeader2016       = New-Object System.Windows.Forms.Label

$lblAdmin            = New-Object System.Windows.Forms.Label

$lblCrawl            = New-Object System.Windows.Forms.Label

$lblContent          = New-Object System.Windows.Forms.Label

$lblIndex            = New-Object System.Windows.Forms.Label

$lblQuery            = New-Object System.Windows.Forms.Label

$lblAnalytics        = New-Object System.Windows.Forms.Label

$lblIndexLocation    = New-Object System.Windows.Forms.Label

$txtIndexLocation    = New-Object System.Windows.Forms.TextBox

 

$lblAdminNew         = New-Object System.Windows.Forms.Label

$lblCrawlNew         = New-Object System.Windows.Forms.Label

$lblContentNew       = New-Object System.Windows.Forms.Label

$lblIndexNew         = New-Object System.Windows.Forms.Label

$lblQueryNew         = New-Object System.Windows.Forms.Label

$lblAnalyticsNew     = New-Object System.Windows.Forms.Label

$lblIndexNewLocation = New-Object System.Windows.Forms.Label

$txtIndexNewLocation = New-Object System.Windows.Forms.TextBox

 

$ddAdmin             = new-object System.Windows.Forms.CheckedListBox

$ddCrawl             = new-object System.Windows.Forms.CheckedListBox

$ddContent           = new-object System.Windows.Forms.CheckedListBox

$ddIndex             = new-object System.Windows.Forms.CheckedListBox

$ddQuery             = new-object System.Windows.Forms.CheckedListBox

$ddAnalytics         = new-object System.Windows.Forms.CheckedListBox

 

$lblMessage          = New-Object System.Windows.Forms.Label

$line                = new-object Drawing.Pen black

 

#CREATE NEW SERVICE APP FORM

$FormCreateNew        = New-Object System.Windows.Forms.Form

$FormCreateNewFlow    = New-Object System.Windows.Forms.flowlayoutpanel

$lblDatabaseServer    = New-Object System.Windows.Forms.Label

$lblDatabaseName      = New-Object System.Windows.Forms.Label

$lblServiceAppName    = New-Object System.Windows.Forms.Label

$lblServiceAcc        = New-Object System.Windows.Forms.Label

$lblServiceAccPass    = New-Object System.Windows.Forms.Label

$lblQryServiceAcc     = New-Object System.Windows.Forms.Label

$lblQryServiceAccPass = New-Object System.Windows.Forms.Label

$lblContentAcc        = New-Object System.Windows.Forms.Label

$lblContentAccPass    = New-Object System.Windows.Forms.Label

 

$txtDatabaseServer    = New-Object System.Windows.Forms.TextBox

$txtDatabaseName      = New-Object System.Windows.Forms.TextBox

$txtServiceAppName    = New-Object System.Windows.Forms.TextBox

$txtServiceAcc        = New-Object System.Windows.Forms.TextBox

$txtServiceAccPass    = New-Object System.Windows.Forms.TextBox

$txtQryServiceAcc     = New-Object System.Windows.Forms.TextBox

$txtQryServiceAccPass = New-Object System.Windows.Forms.TextBox

$txtContentAcc        = New-Object System.Windows.Forms.TextBox

$txtContentAccPass    = New-Object System.Windows.Forms.TextBox

$txtContentAcc        = New-Object System.Windows.Forms.TextBox

$txtContentAccPass    = New-Object System.Windows.Forms.TextBox

$buttonCreateSA       = New-Object System.Windows.Forms.Button

$lblSAMessage         = New-Object System.Windows.Forms.label

 

#endregion Generated Form Objects

 

 

#region Control properties

 

#FORM SETUP

 

#FONTS

$Font        = New-Object System.Drawing.Font("Arial black",12,[System.Drawing.FontStyle]::Italic)

$Fontlbl     = New-Object System.Drawing.Font("Arial",9)

$FontMessage = New-Object System.Drawing.Font("Arial",11,[System.Drawing.FontStyle]::Bold)

$Fontgrd     = New-Object System.Drawing.Font("Arial",9)

$Fontbtn     = New-Object System.Drawing.Font("Arial",11)

 

#FORM

$FormSearch.text          = "ADD / MODIFY SEARCH TOPOLOGY " +$version

$FormSearch.Font          = $Font

$FormSearch.ClientSize    = '1000, 750'

$FormSearch.autoscroll    = $true

$FormSearch.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen

$FormSearch.MaximizeBox   = $false

$FormSearch.icon          = $frmIcon

 

#FORM CREATE NEW

$FormCreateNew.text          = "CREATE SEARCH SERVICE APPLICATION"

$FormCreateNew.Font          = $Font

$FormCreateNew.ClientSize    = '650, 480'

$FormCreateNew.autoscroll    = $true

$FormCreateNew.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen

$FormCreateNew.MaximizeBox   = $false

 

#SETUP SIZING to RE-SIZE

$dp = New-Object System.Drawing.Point

 

#SETUP MESSAGE

$lblMessage.Text        = "Please click IMPORT to fetch your current Topology"

$lblMessage.AutoSize    = $true

#$lblMessage.wrap        = $true

$lblmessage.MaximumSize = $FormSearch.Width - 10

$lblMessage.ForeColor   = 'Blue'

$lblMessage.Font        = $FontMessage

$dp.X = 10

$dp.Y = $FormSearch.ClientSize.Height-40

$lblMessage.Location    = $dp

 

#GRID FONTS

$gridAdmin.Font     = $Fontgrd

$gridCrawl.Font     = $Fontgrd

$gridContent.Font   = $Fontgrd

$gridIndex.Font     = $Fontgrd

$gridQuery.Font     = $Fontgrd

$gridAnalytics.Font = $Fontgrd

 

$ddAdmin.Font       = $Fontgrd

$ddCrawl.Font       = $Fontgrd

$ddContent.Font     = $Fontgrd

$ddIndex.Font       = $Fontgrd

$ddQuery.Font       = $Fontgrd

$ddAnalytics.Font   = $Fontgrd

 

#BUTTONS

 

$buttonCreateNew.text     = "Create New Service Application"

$buttonCreateNew.Font     = $fontbtn

$buttonCreateNew.AutoSize = $true

$buttonCreateNew.Enabled  = $false

$dp.X = 10

$dp.Y = $FormSearch.ClientSize.Height-120

$buttonCreateNew.Location = $dp

 

$buttonImport.text     = "Import"

$buttonImport.Font     = $fontbtn

$buttonImport.AutoSize = $true

$buttonImport.Enabled  = $false

$dp.X =  $buttonCreateNew.Location.X

$dp.Y = $FormSearch.ClientSize.Height-90

$buttonImport.Location = $dp

 

$buttonExit.text     = "Exit"

$buttonExit.Font     = $fontbtn

$buttonExit.AutoSize = $true

$dp.X = $FormSearch.ClientSize.Width-85

$dp.Y = $FormSearch.ClientSize.Height-90

$buttonExit.Location = $dp

 

$buttonUpdate.text     = "Modify Topology"

$buttonUpdate.Font     = $fontbtn

$buttonUpdate.AutoSize = $true

$dp.X = $buttonExit.Location.X -130

$dp.Y = $FormSearch.ClientSize.Height-90

$buttonupdate.Location = $dp

$buttonUpdate.Enabled  = $false

 

#SEARCH SERVICE APPLICATIONS

$lblSSA.Text         = "Search Service Applications"

$lblSSA.Font         = $Fontlbl

$lblSSA.AutoSize     = $true

$dp.X = 10

$dp.Y = 10

$lblSSA.Location     = $dp

 

$ddlSSA.Font         = $Fontgrd

$ddlSSA.Width        = 420

$ddlSSA.AutoSize     = $true

$dp.X = $lblSSA.Location.X + $lblSSA.Width + 70

$dp.Y = $lblSSA.Location.Y

$ddlSSA.Location      =$dp

$ddlssa.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList

 

 

# CURRENT TOPOLOGY CONFIGURATION

#region CURRENT TOPOLOGY

 

#HEADER

$lblHeader.text         = "CURRENT TOPOLOGY"

$lblHeader.Font         = $Font

$lblHeader.AutoSize     = $true

$dp.X = ($FormSearch.ClientSize.Width / 2) - 95

$dp.Y = $lblSSA.Location.Y +30

$lblHeader.Location     = $dp

 

#ADMIN

$lblAdmin.AutoSize = $true

$lblAdmin.Text     = 'ADMIN'

$lblAdmin.Font     = $Fontlbl

# NB - All components positioning is relative to this label so if you want to move items change these only

$dp.X = 10

$dp.Y = 80

$lblAdmin.Location = $dp

 

$gridAdmin.Height  = 180

$gridAdmin.Width   = 155

 

$dp.X = $lblAdmin.Location.X

$dp.Y = $lblAdmin.Location.Y +20

$gridAdmin.Location = $dp

 

#CRAWL

$lblCrawl.AutoSize  = $true

$lblCrawl.Text      = 'CRAWL'

$lblCrawl.Font      = $Fontlbl

$dp.X = $gridAdmin.Location.X + $gridadmin.width + 10

$dp.Y = $lblAdmin.Location.Y

$lblCrawl.Location  = $dp

 

$gridCrawl.Height   = $gridAdmin.Height

$gridCrawl.width    = $gridAdmin.Width

$dp.X = $gridAdmin.Location.X + $gridadmin.width + 10

$dp.Y = $lblCrawl.Location.Y +20

$gridCrawl.Location = $dp

 

#CONTENT

$lblContent.AutoSize = $true

$lblContent.Text     = 'CONTENT PROCESSING'

$lblContent.Font     = $Fontlbl

$dp.X = $gridCrawl.Location.X + $gridCrawl.Width+10

$dp.Y = $lblAdmin.Location.Y

$lblContent.Location = $dp

 

$gridContent.Height   = $gridAdmin.Height

$gridContent.width    = $gridAdmin.Width

$dp.X = $gridCrawl.Location.X + $gridCrawl.Width+10

$dp.Y = $lblContent.Location.Y +20

$gridContent.Location = $dp

 

#INDEX

$lblIndex.AutoSize = $true

$lblIndex.Text     = 'INDEX'

$lblIndex.Font     = $Fontlbl

$dp.X = $gridContent.Location.X + $gridContent.Width+10

$dp.Y = $lblAdmin.Location.Y

$lblIndex.Location = $dp

 

$gridIndex.Height   = $gridAdmin.Height

$gridIndex.width    = $gridAdmin.Width

$dp.X = $gridContent.Location.X + $gridContent.Width+10

$dp.Y = $lblIndex.Location.Y +20

$gridIndex.Location = $dp

 

$lblIndexLocation.text     = "Index Location: "

$lblIndexLocation.Font     = $Fontlbl

$lblIndexLocation.AutoSize = $true

$dp.X = $gridIndex.Location.X

$dp.Y = $gridIndex.Location.Y + $gridIndex.Height + 5

$lblIndexLocation.Location = $dp

 

$txtIndexLocation.Width    = ($gridAdmin.width * 3) + 15

$txtIndexLocation.Font     = $Fontlbl

$txtIndexLocation.ReadOnly = $true

$dp.X = $lblIndexLocation.Location.X

$dp.Y = $lblIndexLocation.Location.Y + 20

$txtIndexLocation.Location = $dp

 

#QUERY

$lblQuery.AutoSize  = $true

$lblQuery.Text      = 'QUERY'

$lblQuery.Font      = $Fontlbl

$dp.X = $gridIndex.Location.X + $gridIndex.Width+10

$dp.Y = $lblAdmin.Location.Y

$lblQuery.Location  = $dp

 

$gridQuery.Height   = $gridAdmin.Height

$gridQuery.width    = $gridAdmin.Width

$dp.X = $gridIndex.Location.X + $gridIndex.Width+10

$dp.Y = $lblQuery.Location.Y +20

$gridQuery.Location = $dp

 

#ANALYTICS

$lblAnalytics.AutoSize = $true

$lblAnalytics.Text     = 'ANALYTICS'

$lblAnalytics.Font     = $Fontlbl

$dp.X = $gridQuery.Location.X + $gridQuery.Width+10

$dp.Y = $lblAdmin.Location.Y

$lblAnalytics.Location = $dp

 

$gridAnalytics.Height   = $gridAdmin.Height

$gridAnalytics.width    = $gridAdmin.Width

$dp.X = $gridQuery.Location.X + $gridQuery.Width+10

$dp.Y = $lblAnalytics.Location.Y +20

$gridAnalytics.Location = $dp

#----------------------------------------------

#endregion CURRENT TOPOLOGY

 

# NEW TOPOLOGY CONFIGURATION

#region NEW TOPOLOGY

 

$lblHeader2.text     = "NEW TOPOLOGY"

$lblHeader2.AutoSize = $true

$lblHeader2.Font = $Font

$dp.X = ($FormSearch.ClientSize.Width / 2) - 75

$dp.Y = ($FormSearch.ClientSize.Height / 2) - 30

$lblHeader2.Location = $dp

 

#2016 message

$lblHeader2016.text     = ""

$lblHeader2016.AutoSize = $true

$lblHeader2016.Font = $Fontlbl

$lblHeader2016.ForeColor = "Red"

$dp.X = $lblHeader2.Location.X - 118

$dp.Y = $lblHeader2.location.Y +30

$lblHeader2016.Location = $dp

 

#ADMIN

 

$lblAdminNew.AutoSize = $true

$lblAdminNew.Text     = 'ADMIN'

$lblAdminNew.Font     = $Fontgrd

$dp.X = $lblAdmin.Location.X

$dp.Y = $lblHeader2016.location.Y +30

$lblAdminNew.Location = $dp

 

$ddAdmin.width        = $gridAdmin.Width

$ddAdmin.height       = $gridAdmin.Height

$ddAdmin.CheckOnClick = $true

$dp.X = $gridAdmin.Location.X

$dp.Y = $lblAdminNew.Location.Y + 20

$ddAdmin.Location     = $dp

 

#CRAWL

$lblCrawlNew.AutoSize = $true

$lblCrawlNew.Text     = 'CRAWL'

$lblCrawlNew.Font     = $Fontlbl

$dp.X = $lblCrawl.Location.X

$dp.Y = $lblAdminNew.Location.Y

$lblCrawlNew.Location = $dp

 

$ddCrawl.Width        = $ddAdmin.Width

$ddCrawl.Height       = $ddAdmin.Height

$ddCrawl.CheckOnClick = $true

$dp.X = $gridCrawl.Location.X

$dp.Y = $ddAdmin.Location.Y

$ddCrawl.Location     = $dp

 

#CONTENT

$lblContentNew.AutoSize = $true

$lblContentNew.Text     = 'CONTENT PROCESSING'

$lblContentNew.Font     = $Fontlbl

$dp.X = $lblContent.Location.X

$dp.Y = $lblAdminNew.location.Y

$lblContentNew.Location = $dp

 

$ddContent.width        = $ddAdmin.width

$ddContent.height       = $ddAdmin.height

$ddContent.CheckOnClick = $true

$dp.X = $gridContent.Location.X

$dp.Y = $ddAdmin.Location.Y

$ddContent.Location     = $dp

 

#INDEX

$lblIndexNew.AutoSize  = $true

$lblIndexNew.Text      = 'INDEX'

$lblIndexNew.Font      = $Fontlbl

$dp.X = $lblIndex.Location.X

$dp.Y = $lblAdminNew.location.Y

$lblIndexNew.Location  = $dp

 

$ddIndex.width        = $ddAdmin.width

$ddIndex.height       = $ddAdmin.height

$ddIndex.CheckOnClick = $true

$dp.X = $gridIndex.Location.X

$dp.Y = $ddAdmin.Location.Y

$ddIndex.Location     = $dp

 

$lblIndexNewLocation.text     = 'Index Location: '

$lblIndexNewLocation.AutoSize = $true

$lblIndexNewLocation.Font     = $Fontlbl

$dp.X = $lblIndex.Location.X

$dp.Y = $ddIndex.Location.Y + $ddIndex.Height +5

$lblIndexNewLocation.Location = $dp

 

$txtIndexNewLocation.Width    = ($ddAdmin.width * 3) + 15

$txtIndexNewLocation.Font     = $Fontlbl

$dp.X = $lblIndexNewLocation.Location.X

$dp.Y = $lblIndexNewLocation.Location.Y + 20

$txtIndexNewLocation.Location = $dp

$txtIndexNewLocation.ReadOnly = $true

 

#QUERY

$lblQueryNew.AutoSize  = $true

$lblQueryNew.Text      = 'QUERY'

$lblQueryNew.Font      = $Fontlbl

$dp.X = $lblquery.Location.X

$dp.Y = $lblAdminNew.location.Y

$lblQueryNew.Location  = $dp

 

$ddQuery.width        = $ddAdmin.width

$ddQuery.height       = $ddAdmin.height

$ddQuery.CheckOnClick = $true

$dp.X = $gridQuery.Location.X

$dp.Y = $ddAdmin.Location.Y

$ddQuery.Location     = $dp

 

#ANALYTICS

$lblAnalyticsNew.AutoSize = $true

$lblAnalyticsNew.Text     = 'ANALYTICS'

$lblAnalyticsNew.Font     = $Fontlbl

$dp.X = $lblAnalytics.Location.X

$dp.Y = $lblAdminNew.location.Y

$lblAnalyticsNew.Location = $dp

 

$ddAnalytics.width        = $ddAdmin.width

$ddAnalytics.height       = $ddAdmin.height

$ddAnalytics.CheckOnClick = $true

$dp.X = $gridAnalytics.Location.X

$dp.Y = $ddAdmin.Location.Y

$ddAnalytics.Location     = $dp

#----------------------------------------------

#endregion NEW TOPOLOGY

 

#region CREATE NEW SERVICE APP

#SERVICE APP

$lblServiceAppName.Text     = "SEARCH SERVICE APPLICATION NAME"

$lblServiceAppName.AutoSize = $true

$lblServiceAppName.Font     = $Fontlbl

$dp.X = 10

$dp.Y = 10

$lblServiceAppName.Location = $dp

 

$txtServiceAppName.Text     = "Search Service Application"

$txtServiceAppName.AutoSize = $true

$txtServiceAppName.width    = 300

$txtServiceAppName.Font     = $Fontlbl

$dp.X = $lblServiceAppName.location.X + 320

$dp.Y = $lblServiceAppName.Location.Y

$txtServiceAppName.Location = $dp

 

#DATABASE SERVER

$lblDatabaseServer.Text     = "DATABASE SERVER"

$lblDatabaseServer.AutoSize = $true

$lblDatabaseServer.Font     = $Fontlbl

$dp.X = $lblServiceAppName.location.X

$dp.Y = $lblServiceAppName.Location.Y + 50

$lblDatabaseServer.Location = $dp

 

$txtDatabaseServer.AutoSize = $true

$txtDatabaseServer.width    = $txtServiceAppName.width

$txtDatabaseServer.Font     = $Fontlbl

$dp.X = $lblDatabaseServer.location.X + 320

$dp.Y = $lblDatabaseServer.Location.Y

$txtDatabaseServer.Location = $dp

 

#DATABASE NAME

$lblDatabaseName.Text     = "DATABASE NAME"

$lblDatabaseName.AutoSize = $true

$lblDatabaseName.Font     = $Fontlbl

$dp.X = $lblDatabaseServer.location.X

$dp.Y = $lblDatabaseServer.Location.Y + 30

$lblDatabaseName.Location = $dp

 

$txtDatabaseName.AutoSize = $true

$txtDatabaseName.width    = $txtServiceAppName.width

$txtDatabaseName.Font     = $Fontlbl

$dp.X = $lblDatabaseServer.location.X + 320

$dp.Y = $lblDatabaseName.Location.Y

$txtDatabaseName.Location = $dp

 

#SEARCH SERVICE ACCOUNT

$lblServiceAcc.Text     = "SEARCH SERVICE ACCOUNT`r`n(e.g.domain\user)"

$lblServiceAcc.AutoSize = $true

$lblServiceAcc.Font     = $Fontlbl

$dp.X = $lblDatabaseName.location.X

$dp.Y = $lblDatabaseName.Location.Y + 60

$lblServiceAcc.Location = $dp

 

$txtServiceAcc.AutoSize = $true

$txtServiceAcc.width    = $txtServiceAppName.width

$txtServiceAcc.Font     = $Fontlbl

$dp.X = $lblServiceAcc.location.X + 320

$dp.Y = $lblServiceAcc.Location.Y

$txtServiceAcc.Location = $dp

 

#SEARCH SERVICE ACC PASSWORD

$lblServiceAccPass.Text     = "SEARCH SERVICE ACCOUNT PASSWORD"

$lblServiceAccPass.AutoSize = $true

$lblServiceAccPass.Font     = $Fontlbl

$dp.X = $lblServiceAcc.location.X

$dp.Y = $lblServiceAcc.Location.Y + 40

$lblServiceAccPass.Location = $dp

 

$txtServiceAccPass.AutoSize  = $true

$txtServiceAccPass.width     = $txtServiceAppName.width

$txtServiceAccPass.Font      = $Fontlbl

$dp.X = $lblServiceAccPass.location.X + 320

$dp.Y = $lblServiceAccPass.Location.Y

$txtServiceAccPass.Location = $dp

 

#SEARCH QUERY APP POOL ACC

$lblQryServiceAcc.Text     = "SEARCH QUERY POOL ACCOUNT`r`n(e.g.domain\user)"

$lblQryServiceAcc.AutoSize = $true

$lblQryServiceAcc.Font     = $Fontlbl

$dp.X = $lblServiceAccPass.location.X

$dp.Y = $lblServiceAccPass.Location.Y + 60

$lblQryServiceAcc.Location = $dp

 

$txtQryServiceAcc.AutoSize = $true

$txtQryServiceAcc.width    = $txtServiceAppName.width

$txtQryServiceAcc.Font     = $Fontlbl

$dp.X = $lblQryServiceAcc.location.X + 320

$dp.Y = $lblQryServiceAcc.Location.Y

$txtQryServiceAcc.Location = $dp

 

#SEARCH QUERY APP POOL PASSWORD

$lblQryServiceAccPass.Text     = "SEARCH QUERY POOL ACCOUNT PASSWORD"

$lblQryServiceAccPass.AutoSize = $true

$lblQryServiceAccPass.Font     = $Fontlbl

$dp.X = $lblQryServiceAcc.location.X

$dp.Y = $lblQryServiceAcc.Location.Y + 40

$lblQryServiceAccPass.Location = $dp

 

$txtQryServiceAccPass.AutoSize = $true

$txtQryServiceAccPass.width    = $txtServiceAppName.width

$txtQryServiceAccPass.Font     = $Fontlbl

$dp.X = $lblQryServiceAccPass.location.X + 320

$dp.Y = $lblQryServiceAccPass.Location.Y

$txtQryServiceAccPass.Location = $dp

 

#SEARCH CONTENT ACC

$lblContentAcc.Text     = "SEARCH CONTENT ACCOUNT`r`n(e.g.domain\user) Do Not use the Farm Account"

$lblContentAcc.AutoSize = $true

$lblContentAcc.Font     = $Fontlbl

$dp.X = $lblQryServiceAccPass.location.X

$dp.Y = $lblQryServiceAccPass.Location.Y + 60

$lblContentAcc.Location = $dp

 

$txtContentAcc.AutoSize = $true

$txtContentAcc.width    = $txtServiceAppName.width

$txtContentAcc.Font     = $Fontlbl

$dp.X = $lblContentAcc.location.X + 320

$dp.Y = $lblContentAcc.Location.Y

$txtContentAcc.Location = $dp

 

#SEARCH CONTENT ACC PASSWORD

$lblContentAccPass.Text     = "SEARCH CONTENT ACCOUNT PASSWORD"

$lblContentAccPass.AutoSize = $true

$lblContentAccPass.Font     = $Fontlbl

$dp.X = $lblContentAcc.location.X

$dp.Y = $lblContentAcc.Location.Y + 40

$lblContentAccPass.Location = $dp

 

$txtContentAccPass.AutoSize = $true

$txtContentAccPass.width    = $txtServiceAppName.width

$txtContentAccPass.Font     = $Fontlbl

$dp.X = $lblContentAccPass.location.X + 320

$dp.Y = $lblContentAccPass.Location.Y

$txtContentAccPass.Location = $dp

 

$buttonCreateSA.text     = "Create Service Application"

$buttonCreateSA.AutoSize = $true

$buttonCreateSA.Font     = $Fontbtn

$buttonCreateSA.Anchor   = "Right"

$dp.X = $FormCreateNew.ClientSize.width-95

$dp.Y = $FormCreateNew.ClientSize.Height-60

$buttonCreateSA.Location = $dp

 

$lblSAMessage.text      = "All fields are mandatory"

$lblSAMessage.autosize  = $true

$lblSAMessage.Font      = $FontMessage

$lblSAMessage.Forecolor = "Blue"

$dp.X = $lblServiceAppName.Location.X

$dp.Y = $FormCreateNew.ClientSize.Height-30

$lblSAMessage.Location  = $dp

 

#$FormCreateNewFlow.font          = $Fontlbl

#$FormCreateNewFlow.AutoSize      = $true

#$FormCreateNewFlow.FlowDirection = "TopDown"

#$FormCreateNewFlow.Padding       = 10

##$FormCreateNewFlow.WrapContents  = $true

 

#endregion CREATE NEW SERVICE APP

 

#region Adding objects to Form

 

$FormSearch.controls.add($buttonCreateNew)

$FormSearch.controls.add($buttonImport)

$FormSearch.controls.add($buttonUpdate)

$FormSearch.Controls.Add($buttonExit)

$FormSearch.Controls.Add($lblSSA)

$FormSearch.Controls.Add($ddlSSA)

 

$FormSearch.Controls.Add($gridAdmin)

$FormSearch.Controls.Add($gridCrawl)

$FormSearch.Controls.Add($gridContent)

$FormSearch.Controls.Add($gridIndex)  

$FormSearch.Controls.Add($gridQuery)  

$FormSearch.Controls.Add($gridAnalytics)

 

$FormSearch.Controls.Add($lblHeader)

$FormSearch.Controls.Add($lblHeader2)

$FormSearch.Controls.Add($lblHeader2016)

$FormSearch.Controls.Add($lblAdmin)

$FormSearch.Controls.Add($lblCrawl)

$FormSearch.Controls.Add($lblContent)

$FormSearch.Controls.Add($lblIndex)   

$FormSearch.Controls.Add($lblQuery)   

$FormSearch.Controls.Add($lblAnalytics)

$FormSearch.Controls.Add($lblIndexLocation)

$FormSearch.Controls.Add($txtIndexLocation)

 

$FormSearch.Controls.Add($lblAdminNew)

$FormSearch.Controls.Add($lblCrawlNew)

$FormSearch.Controls.Add($lblContentNew)

$FormSearch.Controls.Add($lblIndexNew)   

$FormSearch.Controls.Add($lblQueryNew)   

$FormSearch.Controls.Add($lblAnalyticsNew)

$FormSearch.Controls.Add($lblIndexNewLocation)

$FormSearch.Controls.Add($txtIndexNewLocation)

 

$FormSearch.Controls.Add($ddAdmin)    

$FormSearch.Controls.Add($ddCrawl)    

$FormSearch.Controls.Add($ddContent)    

$FormSearch.Controls.Add($ddIndex)    

$FormSearch.Controls.Add($ddQuery)    

$FormSearch.Controls.Add($ddAnalytics)   

 

$FormSearch.Controls.Add($lblMessage)   

    

#FORM-Create New Service App

$FormCreateNew.Controls.Add($lblServiceAppName)

$FormCreateNew.Controls.Add($txtServiceAppName)

 

$FormCreateNew.Controls.Add($lblDatabaseServer)

$FormCreateNew.Controls.Add($txtDatabaseServer)

 

$FormCreateNew.Controls.Add($lblDatabaseName)

$FormCreateNew.Controls.Add($txtDatabaseName)

 

$FormCreateNew.Controls.Add($lblServiceAcc)

$FormCreateNew.Controls.Add($txtServiceAcc)

 

$FormCreateNew.Controls.Add($lblServiceAccPass)

$FormCreateNew.Controls.Add($txtServiceAccPass)

 

$FormCreateNew.Controls.Add($lblQryServiceAcc)

$FormCreateNew.Controls.Add($txtQryServiceAcc)

 

$FormCreateNew.Controls.Add($lblQryServiceAccPass)

$FormCreateNew.Controls.Add($txtQryServiceAccPass)

 

$FormCreateNew.Controls.Add($lblContentAcc)  

$FormCreateNew.Controls.Add($txtContentAcc)

 

$FormCreateNew.Controls.Add($lblContentAccPass)   

$FormCreateNew.Controls.Add($txtContentAccPass)

 

$FormCreateNew.Controls.Add($buttonCreateSA)

$FormCreateNew.Controls.Add($lblSAMessage)

 

#endregion Adding objects to Form

#endregion Control properties

 

function Display-Message ($message, $messageType)

{

    $lblMessage.Text = $message

    $lblMessage.ForeColor = Switch ($messageType) { 1{ "Red" } 2{ "Blue" } 3{ "Green" } }

    $lblMessage.Refresh()

}

 

function Populate-CurrentConfig

{

   try

   {

    #GET CURRENT COMPONENT CONFIG

    #DECLARE ARRAYS TO POPULATE SERVER NAMES

    $arrAdmin          = New-Object System.Collections.ArrayList

    $arrCrawl          = New-Object System.Collections.ArrayList

    $arrContent        = New-Object System.Collections.ArrayList

    $Global:arrIndex   = New-Object System.Collections.ArrayList

    $arrIndexLocation  = New-Object System.Collections.ArrayList

    $arrQuery          = New-Object System.Collections.ArrayList

    $arrAnalytics      = New-Object System.Collections.ArrayList

 

    #DECLARE ARRAYS WITH COMPONENT NAME VALUES

    $Global:arrAdminComp     = New-Object System.Collections.ArrayList

    $Global:arrCrawlComp     = New-Object System.Collections.ArrayList

    $Global:arrContentComp   = New-Object System.Collections.ArrayList

    $Global:arrIndexComp     = New-Object System.Collections.ArrayList

    $Global:arrQueryComp     = New-Object System.Collections.ArrayList

    $Global:arrAnalyticsComp = New-Object System.Collections.ArrayList

    $filename = "$env:temp\TEMP-$(Get-Date -format 'yyyy-MM-dd hh-mm-ss').xml"

    Export-SPEnterpriseSearchTopology -SearchApplication $ddlSSA.SelectedItem -Filename $filename

 

    $Global:searchXML = [Xml] (Get-Content $filename)

    $arrAll = @()

    $arrAll += $Global:searchXML.DocumentElement.Components.Component

    $Global:Components = $arrAll

 

  

    #Populate server names for current config

    $Global:Components | Select Server, Name, Type | where-object {$_.Type -eq $Admin}  | sort -Property Server |%{$arrAdmin.add($_.Server)} | Out-Null

    $Global:Components | Select Server, Name, Type | where-object {$_.Type -eq $Crawl}  | sort -Property Server |%{$arrCrawl.add($_.Server)}  | Out-Null

    $Global:Components | Select Server, Name, Type | where-object {$_.Type -eq $Content} | sort -Property Server |%{$arrContent.add($_.Server)}  | Out-Null

    $Global:Components | Select Server, Name, Type | where-object {$_.Type -eq $Index} | sort -Property Server |%{$Global:arrIndex.add($_.Server)} | Out-Null

 

    $ind = $Components | Select * | where-object {$_.Type -eq $Index}

    #ISSUE WITH DEFAULT ROOT DIRECTORY

    # C:\Program Files\Microsoft Office Servers\15.0\Data\Office Server\Applications\

    # If the default root directory is used when creating the Search Service Application then the value returned is NULL

    try

    {

        $indexRoot = ($ind.Property | where-object {$_.Key -eq "RootDirectory"} | select value)[0].Value

    }

    catch

    {

        #$indexroot = "C:\Program Files\Microsoft Office Servers\15.0\Data\Office Server\Applications\"

        $indexroot = "DEFAULT"

    }

    $txtIndexLocation.Text = $indexRoot

    #DEFAULT NEW INDEX LOCATION TO CURRENT

    $txtIndexNewLocation.Text = $txtIndexLocation.Text

    # Index Location change requires a separate SET Process

 

    $Global:Components | Select Server, Name, Type | where-object {$_.Type -eq $Query} | sort -Property Server |%{$arrQuery.add($_.Server)}  | Out-Null

    $Global:Components | Select Server, Name, Type | where-object {$_.Type -eq $Analytics} | sort -Property Server |%{$arrAnalytics.add($_.Server)}  | Out-Null

   

    #Populate Component names for current config to minimise changes when updating the topology

    $Global:Components | Select Server, Name, Type | where-object {$_.Type -eq $Admin}  | sort -Property Server |%{$Global:arrAdminComp.add($_)}  | Out-Null

    $Global:Components | Select Server, Name, Type | where-object {$_.Type -eq $Crawl}  | sort -Property Server |%{$Global:arrCrawlComp.add($_)}  | Out-Null

    $Global:Components | Select Server, Name, Type | where-object {$_.Type -eq $Content} | sort -Property Server |%{$Global:arrContentComp.add($_)}  | Out-Null

    $Global:Components | Select Server, Name, Type | where-object {$_.Type -eq $Index} | sort -Property Server |%{$Global:arrIndexComp.add($_)}  | Out-Null

    $Global:Components | Select Server, Name, Type | where-object {$_.Type -eq $Query} | sort -Property Server |%{$Global:arrQueryComp.add($_)}  | Out-Null

    $Global:Components | Select Server, Name, Type | where-object {$_.Type -eq $Analytics} | sort -Property Server |%{$Global:arrAnalyticsComp.add($_)}   | Out-Null 

 

    $gridAdmin.DataSource     = $arrAdmin

    $gridAdmin.SelectedItem = $null

 

    $gridCrawl.DataSource     = $arrCrawl   

    $gridCrawl.SelectedItem = $null

 

    $gridContent.DataSource   = $arrContent

    $gridContent.SelectedItem = $null

 

    $gridIndex.DataSource     = $Global:arrIndex  

    $gridIndex.SelectedItem = $null

 

    $gridQuery.DataSource     = $arrQuery    

    $gridQuery.SelectedItem = $null

 

    $gridAnalytics.DataSource = $arrAnalytics

    $gridAnalytics.SelectedItem = $null

 

 

    populate-ServerDetails

    #SET ADMIN

    for ($i=0;$i -lt $ddAdmin.Items.Count;$i++)

    {

        #Reset first

        if($arrAdmin.IndexOf($ddAdmin.Items[$i]) -ge 0)

        {

            $item = $ddadmin.SetItemChecked($i,$true)

        }

        else

        {

            $item = $ddadmin.SetItemChecked($i,$false)

        }

    }

    $ddAdmin.SelectedItem = $null

 

    #SET CRAWL

    for ($i=0;$i -lt $ddCrawl.Items.Count;$i++)

    {

        if($arrCrawl.IndexOf($ddCrawl.Items[$i]) -ge 0)

        {

            $item = $ddCrawl.SetItemChecked($i,$true)

        }

        else

        {

            $item = $ddCrawl.SetItemChecked($i,$false)

        }

    }

    $ddCrawl.SelectedItem = $null

 

 

    #SET CONTENT

    for ($i=0;$i -lt $ddContent.Items.Count;$i++)

    {

        if($arrContent.IndexOf($ddContent.Items[$i]) -ge 0)

        {

            $item = $ddContent.SetItemChecked($i,$true)

        }

        else

        {

            $item = $ddContent.SetItemChecked($i,$false)

        }

    }

    $ddContent.SelectedItem = $null

 

 

    #SET INDEX

    for ($i=0;$i -lt $ddIndex.Items.Count;$i++)

    {

        if($Global:arrIndex.IndexOf($ddIndex.Items[$i]) -ge 0)

        {

            $item = $ddIndex.SetItemChecked($i,$true)

        }

        else

        {

            $item = $ddIndex.SetItemChecked($i,$false)

        }

    }

    $ddIndex.SelectedItem = $null

 

    #SET QUERY

    for ($i=0;$i -lt $ddQuery.Items.Count;$i++)

    {

        if($arrQuery.IndexOf($ddQuery.Items[$i]) -ge 0)

        {

            $item = $ddQuery.SetItemChecked($i,$true)

       }

        else

        {

            $item = $ddQuery.SetItemChecked($i,$false)

        }

    }

    $ddQuery.SelectedItem = $null

 

    #SET ANALYTICS

    for ($i=0;$i -lt $ddAnalytics.Items.Count;$i++)

    {

        if($arrAnalytics.IndexOf($ddAnalytics.Items[$i]) -ge 0)

        {

            $item = $ddAnalytics.SetItemChecked($i,$true)

        }

        else

        {

            $item = $ddAnalytics.SetItemChecked($i,$false)

        }

    }

    $ddAnalytics.SelectedItem = $null

 

    Display-Message "Topology Imported succesfully and pre-selected in the new Topology. Please carefully select your new topology" $InfoMessage

   

    #Enable Modify button

    $buttonUpdate.Enabled = $true

    $txtIndexNewLocation.ReadOnly = $false

 

    $FormSearch.Refresh()

    }

    catch

    {

        Display-Message "Error loading the Topology" $errMessage

    }

}

 

function Populate-ServerDetails

{

    $ddAdmin.DataSource     = $Global:SPservers

    Reset-ddItems $ddAdmin $false

    $ddContent.DataSource   = $Global:SPservers

    Reset-ddItems $ddContent $false

    $ddCrawl.DataSource     = $Global:SPservers

    Reset-ddItems $ddCrawl $false

    $ddIndex.DataSource     = $Global:SPservers

    Reset-ddItems $ddIndex $false

    $ddQuery.DataSource     = $Global:SPservers

    Reset-ddItems $ddQuery $false

    $ddAnalytics.DataSource = $Global:SPservers

    Reset-ddItems $ddAnalytics $false

}

 

function Reset-ddItems ($dropdown, $remove)

{

    for ($i=0;$i -lt $dropdown.Items.Count;$i++)

    {

        if($remove)

        {

            $dropdown.datasource = $null

            $dropdown.items.remove($i)

        }

        else

        {

            $dropdown.SetItemChecked($i,$false)

        }

    }

}

 

function StartStop-AllServices

{

    try

    {

        Display-Message "Checking and Starting the Services" $StatusMessage

        #$FormSearch.Refresh()

 

        #Get Servers to be used

        $newServers =  New-Object System.Collections.ArrayList

 

        $ddAdmin.CheckedItems | %{$newServers.add($_)} | Out-Null

        $ddCrawl.CheckedItems | %{$newServers.add($_)} | Out-Null

        $ddContent.CheckedItems | %{$newServers.add($_)} | Out-Null

        $ddIndex.CheckedItems | %{$newServers.add($_)} | Out-Null

        $ddQuery.CheckedItems | %{$newServers.add($_)} | Out-Null

        $ddAnalytics.CheckedItems | %{$newServers.add($_)} | Out-Null

 

        $newServers = $newServers | SELECT -Unique | Sort

 

        #######################################

        #region Code Snippet from Joe Rodgers

        #Start all the Search instances on all Selected Services

        Get-SPEnterpriseSearchServiceInstance | ? { $newServers -contains $_.Server.Name -and $_.Status -ne "Online" } `

        | % {

                Display-Message ("Checking and Starting Search Service Instance on "+$_.Server.Name) $StatusMessage

                $_ | Start-SPEnterpriseSearchServiceInstance

                #$FormSearch.Refresh()

            }

 

        # wait for all instances to finish provisioning

        do

        {

            Start-Sleep -Seconds 5

            $instances = Get-SPEnterpriseSearchServiceInstance | ? { $newServers -contains $_.Server.Name -and $_.Status -ne "Online" }

        }

        while($instances)

        Get-SPEnterpriseSearchServiceInstance | ? { $newServers -contains $_.Server.Name } `

        | % {

                Display-Message ("Search Service Instance on "+$_.Server.Name+ " "+$_.Status) $StatusMessage

            }

 

        # We need to restart 3 Windows services on all the search instances so that they get a new security token including the WSS_WPG group

        # http://blogs.msdn.com/b/briangre/archive/2014/01/29/least-privilege-remote-configuration-of-search-for-sharepoint-server-2013.aspx

        Get-SPEnterpriseSearchServiceInstance | ? { $allComponentServers -contains $_.Server.Name } | Sort Server | % {

 

        $netTcpActivatorSvc   = Get-WmiObject -Class Win32_Service -ComputerName $_.Server.Name | ? { $_.Name -eq "NetTcpActivator" }

        $netTcpPortSharingSvc = Get-WmiObject -Class Win32_Service -ComputerName $_.Server.Name | ? { $_.Name -eq "NetTcpPortSharing" }

        $netPipeActivatorSvc  = Get-WmiObject -Class Win32_Service -ComputerName $_.Server.Name | ? { $_.Name -eq "NetPipeActivator" }

 

        Display-Message ("Restarting WCF Hosting Services on: "+$_.Server.Name) $StatusMessage

 

        $result = $netTcpActivatorSvc.StopService()

        $result = $netTcpPortSharingSvc.StopService()

        $result = $netPipeActivatorSvc.StopService()

       

        # give the services 2 seconds to shutdown

        Start-Sleep -Seconds 2

 

        # Start the NetTcpPortSharing service on the search instance

        $result = $netTcpPortSharingSvc.StartService()

       

        do

        {

            Start-Sleep -Seconds 5

            $netTcpPortSharingSvc = Get-WmiObject -Class Win32_Service -ComputerName $_.Server.Name | ? { $_.Name -eq "NetTcpPortSharing" }

        }

        while($netTcpPortSharingSvc.State -ne "Running")

 

        # Start the NetTcpActivator service on the search instance

        $result = $netTcpActivatorSvc.StartService()

       

        do

        {

            Start-Sleep -Seconds 5

            $netTcpActivatorSvc = Get-WmiObject -Class Win32_Service -ComputerName $_.Server.Name | ? { $_.Name -eq "NetTcpActivator" }

        }

        while($netTcpActivatorSvc.State -ne "Running")

 

        # Start the NetPipeActivator service on the search instance

        $result = $netPipeActivatorSvc.StartService()

       

        do

        {

            Start-Sleep -Seconds 5

            $netPipeActivatorSvc = Get-WmiObject -Class Win32_Service -ComputerName $_.Server.Name | ? { $_.Name -eq "NetPipeActivator" }

        }

        while($netPipeActivatorSvc.State -ne "Running")

        }

        #End of KB fix

 

        #Start the Query and Site Settings Service Instances on all Servers selected

        $QueryServiceInstanceServers = New-Object System.Collections.ArrayList

        $ddQuery.CheckedItems | %{$QueryServiceInstanceServers.add($_)} | Out-Null

       

        Get-SPEnterpriseSearchQueryAndSiteSettingsServiceInstance | ? { $QueryServiceInstanceServers -contains $_.Server.Name -and $_.Status -ne "Online" }`

         | % {

                Display-Message ("Checking and Starting Query Service Instance on "+$_.Server.Name) $StatusMessage       

                $_ | Start-SPEnterpriseSearchQueryAndSiteSettingsServiceInstance

                #$FormSearch.Refresh()

            }

 

        # wait for all instances to finish provisioning

        do

        {

            Start-Sleep -Seconds 5

            $instances = Get-SPEnterpriseSearchQueryAndSiteSettingsServiceInstance | ? { $QueryServiceInstanceServers -contains $_.Server.Name -and $_.Status -ne "Online" }

        }

        while($instances)

        Get-SPEnterpriseSearchQueryAndSiteSettingsServiceInstance | ? { $QueryServiceInstanceServers -contains $_.Server.Name } `

        | % {

                Display-Message ("Query Service Instance on "+$_.Server.Name+" "+$_.Status) $StatusMessage

            }

 

        #endregion Code Snippet from Joe Rodgers

        ##################################################

    }

    catch

    {

        Display-Message $_ $errMessage

        throw "Error starting services on all Servers"

    }

}

 

function Create-NewTopology

{

    try

    {

        #Set Search Properties - if a New Account is selected then check for other Service applications before just changing it.

        # if a change is done then all current Index components will go offline and an Index Reset and crawl will be required.

        if ($global:isNewSA)

        {

            # Set Search Service Credentials

            $SearchService = Get-SPEnterpriseSearchService

            if ($SearchService)

            {

                $searchsvccredentialschanged = $false

                #Validate the Search Service Account

                # Could just use the already populated drop down but don't want to trust that this is the only config being performed

                $CountSA = Get-SPEnterpriseSearchServiceApplication

                If ($CountSA.count -gt 1)

                {

                    #Have to check the Search Service Account as if there are other service applications

                    #  their Index's will be broken if the account is changed

                   if ($SearchService.ProcessIdentity -ne $txtServiceAcc.Text)

                   {

                        if ([Windows.Forms.MessageBox]::Show("You have existing Service Applications and you have changed the Search Service Account`r`nIf you continue you will have to INDEX RESET and CRAWL all content on those Service Applications.`r`n`r`nAre you sure you want to change the account?`r`n`r`nSelect No to use the existing account: " +$SearchService.ProcessIdentity, "SEARCH SERVICE ACCOUNT CHANGE", [Windows.Forms.MessageBoxButtons]::YesNo) -eq "Yes")

                        {

                            $searchsvccredentialschanged = $true

                        }

                   }

                 }

                 else

                 { # No Existing Service Applications so Account can be changed

                     $searchsvccredentialschanged = $true

                 }

                Display-Message ("Setting Search default Properties") $StatusMessage

                #$FormSearch.Refresh()

 

                if ($searchsvccredentialschanged)

                {#Credentials must be changed or there are no running service applications

                    Set-SPEnterpriseSearchService -Identity $SearchService -ServiceAccount $txtServiceAcc.Text -ServicePassword $(ConvertTo-SecureString -String $txtServiceAccPass.Text -AsPlainText -Force) -ProxyType "Default" -PerformanceLevel "PartlyReduced" -InternetIdentity "Mozilla/4.0 (compatible; MSIE 4.01; Windows NT; MS Search 6.0 Robot)"

                }

                else

                {

                    Set-SPEnterpriseSearchService -Identity $SearchService -ProxyType "Default" -PerformanceLevel "PartlyReduced" -InternetIdentity "Mozilla/4.0 (compatible; MSIE 4.01; Windows NT; MS Search 6.0 Robot)"

                }

 

                if(!$?)

                {

                    throw "Error setting the Credentials and default settings on the Search Service"

                }

            }

 

            Display-Message "Setting Search Content Account credentials" $StatusMessage

            #$FormSearch.Refresh()

            #New Search Service App - Set the Search Query Service App Pool and Content Access Account

            Set-SPEnterpriseSearchServiceApplication –Identity $ddlSSA.SelectedItem -DefaultContentAccessAccountName $txtContentAcc.Text -DefaultContentAccessAccountPassword (ConvertTo-SecureString -String $txtContentAccPass.Text -AsPlainText -Force)

        }

 

 

        #Get the SearchService application

        $searchApp = Get-SPEnterpriseSearchServiceApplication $ddlSSA.SelectedItem

 

        #SECTION INSERTED TO FIX BROKEN TOPOLOGIES

        $isRepair = $false

        if (!$global:isNewSA)

        {

           $isRepair = Check-AdminComponent $searchApp

        }

 

        #If it's a new Service App or a broken Topology then initializing the admin component allows new topology to be created

        if ($global:isNewSA -or $isRepair)

        {

            Display-Message "New Search Service application - Admin Component must be initialized" $StatusMessage

 

            Initialize-AdminComponent $searchApp

       }

 

        #Reset the Topology variables again

        $clone = $null

        $acive = $null

 

        Display-Message "Cloning the Current Topology" $StatusMessage

 

        #CLONE CURRENT

        # Get the Current Topology again in case an Admin Component was added

        $active = Get-SPEnterpriseSearchTopology -SearchApplication $ddlSSA.SelectedItem -Active

        $clone = New-SPEnterpriseSearchTopology -SearchApplication $ddlSSA.SelectedItem -Clone -SearchTopology $active

 

        if ($global:isNewSA)

        {

            #If it's a new one then there will no existing Topology except for the Admin initialized earlier

            # Remove the Admin Component in the Cloned Topology

              #ADMIN

            #foreach ($newCompA in $ddAdmin.CheckedItems)

            #{

            #    $searchInst = Get-SPEnterpriseSearchServiceInstance -Identity $newCompA

            #

            #    # set the admin component on target server   

            #        $ComponenttoRemove = $clone.GetComponents() | where {$_.Name -eq $compA.Name}

            #        Remove-SPEnterpriseSearchComponent -Identity $ComponenttoRemove -SearchTopology $clone -Confirm:$false | Out-Null

            #}

        }

        else

        {

            #==================================================================================

            # Remove any topology component selected to be removed

            #==================================================================================

   

            Display-Message "Removing any deselected Components from the Topology" $StatusMessage

 

            #ADMIN

            foreach ($compA in $Global:arrAdminComp)

            {

                if($ddAdmin.CheckedItems.IndexOf($compA.Server) -ge 0)

                {

                    #LEAVE IT ALONE

                }

                else

                {

                    $ComponenttoRemove = $clone.GetComponents() | where {$_.Name -eq $compA.Name}

                    Remove-SPEnterpriseSearchComponent -Identity $ComponenttoRemove -SearchTopology $clone -Confirm:$false | Out-Null

                }

            }

            #CRAWL

            foreach ($compC in $Global:arrCrawlComp)

            {

                if($ddCrawl.CheckedItems.IndexOf($compC.Server) -ge 0)

                {

                    #LEAVE IT ALONE

                }

                else

                {

                    $ComponenttoRemove = $clone.GetComponents() | where {$_.Name -eq $compC.Name}

                    Remove-SPEnterpriseSearchComponent -Identity $ComponenttoRemove -SearchTopology $clone -Confirm:$false | Out-Null

                }

            }

     

            #CONTENT PROCESSING

            foreach ($compCO in $Global:arrContentComp)

            {

                if($ddContent.CheckedItems.IndexOf($compCO.Server) -ge 0)

                {

                    #LEAVE IT ALONE

                }

                else

                {

                    $ComponenttoRemove = $clone.GetComponents() | where {$_.Name -eq $compCO.Name}

                    Remove-SPEnterpriseSearchComponent -Identity $ComponenttoRemove -SearchTopology $clone -Confirm:$false | Out-Null

                }

            }

           

            #INDEX

            $IndexLocationstoRemove = New-Object System.Collections.ArrayList

            foreach ($compI in $Global:arrIndexComp)

            {

                if($ddIndex.CheckedItems.IndexOf($compI.Server) -ge 0)

                {

                    #LEAVE IT ALONE

                }

                else

                {

                    $ComponenttoRemove = $clone.GetComponents() | where {$_.Name -eq $compI.Name}

                    Remove-SPEnterpriseSearchComponent -Identity $ComponenttoRemove -SearchTopology $clone -Confirm:$false | Out-Null

                    $ComponenttoRemove | %{$IndexLocationstoRemove.add($_)} | Out-Null

                }

            }

        

            #QUERY PROCESSING

            foreach ($compQ in $Global:arrQueryComp)

            {

                if($ddQuery.CheckedItems.IndexOf($compQ.Server) -ge 0)

                {

                    #LEAVE IT ALONE

                }

                else

                {

                    $ComponenttoRemove = $clone.GetComponents() | where {$_.Name -eq $compQ.Name}

                    Remove-SPEnterpriseSearchComponent -Identity $ComponenttoRemove -SearchTopology $clone -Confirm:$false | Out-Null

                }

            }   

 

            #ANALYTICS

            foreach ($compAN in $Global:arrAnalyticsComp)

            {

                if($ddAnalytics.CheckedItems.IndexOf($compAN.Server) -ge 0)

                {

                    #LEAVE IT ALONE

                }

                else

                {

                    $ComponenttoRemove = $clone.GetComponents() | where {$_.Name -eq $compAN.Name}

                    Remove-SPEnterpriseSearchComponent -Identity $ComponenttoRemove -SearchTopology $clone -Confirm:$false | Out-Null

                }

            }   

        }

        #==================================================================================

        # Set where you would like which topology to be created for new items only

        #==================================================================================

        Display-Message "Adding any New Components to the Topology" $StatusMessage

       

        

        #ADMIN

        foreach ($newCompA in $ddAdmin.CheckedItems)

        {

            $CompNameA = $Global:arrAdminComp | Select Name, Server | where-object {$_.Server -eq $newCompA}

            #Check if its still in the topology

            if ($clone.GetComponents() | where {$_.Name -eq $CompNameA.Name})

            {

                # IT EXISTS - Leave it for now

            }

            Else

            {

                $searchInst = Get-SPEnterpriseSearchServiceInstance -Identity $newCompA

                New-SPEnterpriseSearchAdminComponent -SearchTopology $clone -SearchServiceInstance $searchInst | Out-Null

            }

        }

       

    

 

        #CRAWL   

        foreach ($newCompC in $ddCrawl.CheckedItems)

        {

            $CompNameC = $Global:arrCrawlComp | Select Name, Server | where-object {$_.Server -eq $newCompC}

            #Check if its still in the topology

            if ($clone.GetComponents() | where {$_.Name -eq $CompNameC.Name})

            {

                # IT EXISTS - Leave it for now

            }

            Else

            {

                $searchInst = Get-SPEnterpriseSearchServiceInstance -Identity $newCompC

                New-SPEnterpriseSearchCrawlComponent -SearchTopology $clone -SearchServiceInstance $searchInst  | Out-Null

            }

        }

     

        #CONTENT PROCESSING

        foreach ($newCompCO in $ddContent.CheckedItems)

        {

            $CompNameCO = $Global:arrContentComp | Select Name, Server | where-object {$_.Server -eq $newCompCO}

            #Check if its still in the topology

            if ($clone.GetComponents() | where {$_.Name -eq $CompNameCO.Name})

            {

                # IT EXISTS - Leave it for now

            }

            Else

            {

                $searchInst = Get-SPEnterpriseSearchServiceInstance -Identity $newCompCO

                New-SPEnterpriseSearchContentProcessingComponent -SearchTopology $clone -SearchServiceInstance $searchInst | Out-Null

            }

        }

   

        #INDEX

        foreach ($newCompI in $ddIndex.CheckedItems)

        {

            $CompNameI = $Global:arrIndexComp | Select Name, Server | where-object {$_.Server -eq $newCompI}

            #Check if its still in the topology

            if (($clone.GetComponents() | where {$_.Name -eq $CompNameI.Name}) -and !$Global:isIndexLocationchanged)

            {

                # IT EXISTS - Leave it for now

            }

            Else

            {

                $searchInst = Get-SPEnterpriseSearchServiceInstance -Identity $newCompI

                #if ($global:isNewSA -or $Global:isIndexLocationchanged)

                if ($txtIndexNewLocation.text -ne "DEFAULT")

                {#New Index component with a specified index location so always execute unless DEFAULT has been left

                    #Add new index component to replicate it for location change

                    # Known work around for root directory issue

                    $IndexComponent = (New-Object Microsoft.Office.Server.Search.Administration.Topology.IndexComponent $newCompI,0)

                    $IndexComponent.RootDirectory = $txtIndexNewLocation.text

                    $clone.AddComponent($IndexComponent)

                }

                else

                {

                    New-SPEnterpriseSearchIndexComponent -SearchTopology $clone -SearchServiceInstance $searchInst | Out-Null

                }

            }

        }

   

        #QUERY PROCESSING

        foreach ($newCompQ in $ddQuery.CheckedItems)

        {

            $CompNameQ = $Global:arrQueryComp | Select Name, Server | where-object {$_.Server -eq $newCompQ}

            #Check if its still in the topology

            if ($clone.GetComponents() | where {$_.Name -eq $CompNameQ.Name})

            {

                # IT EXISTS - Leave it for now

            }

            Else

            {

                $searchInst = Get-SPEnterpriseSearchServiceInstance -Identity $newCompQ

                New-SPEnterpriseSearchQueryProcessingComponent -SearchTopology $clone -SearchServiceInstance $searchInst | Out-Null

            }

        }

        #ANALYTICS

        foreach ($newCompAN in $ddAnalytics.CheckedItems)

        {

            $CompNameAN = $Global:arrAnalyticsComp | Select Name, Server | where-object {$_.Server -eq $newCompAN}

            #Check if its still in the topology

            if ($clone.GetComponents() | where {$_.Name -eq $CompNameAN.Name})

            {

                # IT EXISTS - Leave it for now

            }

            Else

            {

                $searchInst = Get-SPEnterpriseSearchServiceInstance -Identity $newCompAN

                New-SPEnterpriseSearchAnalyticsProcessingComponent -SearchTopology $clone -SearchServiceInstance $searchInst | Out-Null

            }

        }

 

        #==================================================================================

        # activate the new topology

        #==================================================================================

        Display-Message "Activating new Topology - Be Patient as this takes time" $StatusMessage

 

        #Set-SPEnterpriseSearchTopology -Identity $clone

        $clone.activate()

 

        Display-Message "New Topology Activation Completed - Cleaning Topology and applying final settings" $StatusMessage  

        

        #==================================================================================

        # Clean out inactive topologies

        #==================================================================================

 

        Remove-SPEnterpriseSearchTopology -Identity $active -Confirm:$false;

 

        if ($global:isIndexLocationChanged)

        {

            Remove-OldIndex

        }

        elseif ($IndexLocationstoRemove.Count -gt 0)

        { #Remove the Index folder for removed index Items

            Display-Message "Remove Index Location from Index server change" $StatusMessage

            $scripttoremove = $ExecutionContext.InvokeCommand.NewScriptBlock("Remove-Item -Recurse -Force -LiteralPath " + $txtIndexLocation.Text + " -ErrorAction SilentlyContinue")

            $IndexLocationstoRemove.ServerName | %{

            Invoke-Command -ComputerName $_ -ScriptBlock $scripttoremove}

        }

 

        Display-Message "New Topology Activated and the old Topology has been deleted" $StatusMessage  

        

        $global:isNewSA                = $false

        $Global:isIndexLocationchanged = $false

        $ddlSSA.Enabled = $true

 

        [Windows.Forms.MessageBox]::Show("New Topology has been activated - please review changes under the Search Service Application", "TOPOLOGY MODIFICATION SUCCESSFUL", [Windows.Forms.MessageBoxButtons]::Ok)

    }

    catch

    {

        [Windows.Forms.MessageBox]::Show("Error - New Topology has failed", "TOPOLOGY MODIFICATION FAILED", [Windows.Forms.MessageBoxButtons]::Ok)

        Display-Message $_ $errMessage

    }

}

 

function Check-AdminComponent ($searchApp)

{

    $isRepair = $false

    #If there is no Admin Component then set it to repair one to ensure it does not fail when trying to fix / change the Topology

    $currentAdminComponent =  Get-SPEnterpriseSearchAdministrationComponent -SearchApplication $searchApp

    if (!$? -or !$currentAdminComponent)

    {

        $isRepair = $true

    }

    elseif (!$currentAdminComponent.Initialized)

    {

        $isRepair = $true

    }

    return $isRepair

}

 

function Initialize-AdminComponent ($searchApp)

{

 

    #####################################

    #region Extract from Joe Rodgers

    #Changes have been made to the original extract

 

    $globalTimeoutSeconds = 300 #Set to 5 mins now - changed from 20 minutes max

 

    #ADMIN

    foreach ($newCompA in $ddAdmin.CheckedItems)

    {

        $searchInst = Get-SPEnterpriseSearchServiceInstance -Identity $newCompA

 

        # set the admin component on target server   

        $searchApp | Get-SPEnterpriseSearchAdministrationComponent | Set-SPEnterpriseSearchAdministrationComponent -SearchServiceInstance $searchInst

    }

   

    Display-Message "Waiting for the Admin Component to Initialize" $StatusMessage

 

    # get the admin component - Only need the primary one to be online

    $adminComponent = $searchApp | Get-SPEnterpriseSearchAdministrationComponent

   

    # set the timeout

    $timeoutTime = (Get-Date).AddMinutes($globalTimeoutSeconds)

   

    do

    {

        Start-Sleep -Seconds 5

    }

    while (!$adminComponent.Initialized -and $timeoutTime -ge (Get-Date))

   

 

    if(!$adminComponent.Initialized)

    {

        throw "Admin component failed to initialize on server:  $($adminComponent.Server)"

    }

 

    if(!$?)

    {

        throw $error[0].Tostring()

    }

 

    #endregion Extract from Joe Rodgers

    ################################################

}

 

function Generate-Topology

{

    try

    {

       #CHECK THAT AT LEAST ONE ITEM HAS BEEN SELECTED

       If ($ddAdmin.CheckedItems.Count -gt 0 -and $ddCrawl.CheckedItems.Count -gt 0 `

       -and $ddContent.CheckedItems.Count -gt 0 -and $ddIndex.CheckedItems.Count -gt 0 `

       -and $ddQuery.CheckedItems.Count -gt 0 -and $ddAnalytics.CheckedItems.Count -gt 0)

       {#New check to help fix broken topologies - Index may not have been created yet

            if(!$Global:isNewSA -and $gridIndex.Items.count -gt 0)

            { #For a New SSA there would be no Index Components yet so bypass here

                $Found = $false

                #CODE - re-look at this loop to optimise later

                foreach($selected in $ddIndex.CheckedItems)

                {

                    foreach($old in $Global:arrIndex)

                    {

                        If ($selected -eq $old)

                        {

                            $Found = $true

                        }

                     }

                }

            }

            else

            {

                $Found = $true

            }     

           if($Found)

           {

                #CHECK for PowerShell remote execution

                # This is needed for the Index Location folder creation.

                #Get Servers to be used

                $ServersRM = New-Object System.Collections.ArrayList

                $ddAdmin.CheckedItems | %{$ServersRM.add($_)} | Out-Null

                $ddCrawl.CheckedItems | %{$ServersRM.add($_)} | Out-Null

                $ddContent.CheckedItems | %{$ServersRM.add($_)} | Out-Null

                $ddIndex.CheckedItems | %{$ServersRM.add($_)} | Out-Null

                $ddQuery.CheckedItems | %{$ServersRM.add($_)} | Out-Null

                $ddAnalytics.CheckedItems | %{$ServersRM.add($_)} | Out-Null

 

                $scriptcheckwinrm = $ExecutionContext.InvokeCommand.NewScriptBlock("get-service winrm")

 

                $ServersRM | select-object -unique | %{

                    $checkWinRM = Invoke-Command -ComputerName $_ -ScriptBlock $scriptcheckwinrm

                    if ($checkWinRM.Status -ne "Running")

                    {

                        throw "Enable remote PowerShell on all SharePoint servers. Run 'Enable-PSRemoting –force' and 'WINRM quickconfig' on EACH server"

                    }

                }

 

               if ($Global:isNewSA)

               {

                    $startMessage = "This will create a new Search Service Application.  `r`n `r`nPlease do not cancel the creation process."

               }

               Else

               {

                    $startMessage = "Are you sure you want to modify the Topology? `r`n `r`nSearch will go offline during this time.`r`n`r`nIf the Index location has changed then several topology modifications will be completed."

               }

               if ([Windows.Forms.MessageBox]::Show($startMessage, "MODIFY TOPOLOGY", [Windows.Forms.MessageBoxButtons]::YesNo) -eq "Yes")

               {

                   Display-Message "This will take a long time so please wait until it has completed running" $StatusMessage

                   #$FormSearch.Refresh()

                   $buttonUpdate.Enabled = $false

                                     

                   #ALL other checks complete, now validate Index location for New SSA and for Index move

                   if ($Global:isNewSA -or $Global:isIndexLocationchanged)

                   {

                       if ($Global:isIndexLocationchanged -and $txtIndexLocation.Text -ne "DEFAULT" -and [Windows.Forms.MessageBox]::Show("Old Index Location " + $txtIndexLocation.Text  +" will be Deleted!`r`n`r`nContinue?", "MODIFY INDEX LOCATION", [Windows.Forms.MessageBoxButtons]::YesNo) -eq "No")

                       {

                        throw "Topology Change cancelled by User"

                       }

                       #Validate Index location first.

                       Create-NewindexLocation

 

                       #Start all services

                       StartStop-AllServices

                       # New SSA so create it now.

                       if ($Global:isNewSA){Create-NewServiceApplication}

                   }

                   else

                   {#Not a new SSA but security lockdown on servers requires folder and folder rights be updated manually

                       Create-NewindexLocation

                       #Start all services

                       StartStop-AllServices

 

                   }

 

                   Create-NewTopology

                }

                else

                {

                    Display-Message "You cancelled modifying the Topology" $InfoMessage

                }

           }

           else

           {

                    Display-Message "At least 1 original Index component above must be selected. The Index can only be moved and not deleted" $errMessage

           }

       }

       else

       {

            Display-Message "At least 1 Server must be selected for each Component" $errMessage

       }

    }

    catch

    {

            Display-Message $_ $errMessage

       

    }

}

 

#GET SEARCH SERVICE APPLICATIONS

# Done here so that the topology fetch is completed at Import

function Load-SearchServiceApplication

{

    try

    {

        #GET SERVERS IN FARM

        #region Get Servers

        try

        {

            $Global:SPservers = New-Object System.Collections.ArrayList

 

            $version = Get-SPFarm

            if ($version.buildversion.major -lt 15)

            {

                throw "SharePoint 2013 or higher farm not detected. Please run this on a SharePoint server."

            }

            elseif ($version.buildversion.major -gt 15)

            {# SP2016 - Only "Special Load, Search and Single Server can host Search Components"

                $lblHeader2016.text = "(NB: SP2016 - Only Special Load / Custom, Search and Single Server can host Search Components)"

                Get-SPServer | ? {$_.Role -eq "Application" -or $_.Role -eq "Search" -or $_.Role -eq "SpecialLoad" -or $_.Role -eq "Custom" -or $_.Role -eq "SingleServerFarm"} | %{$Global:SPservers.add($_.Address)} | Out-Null

 

            }

            else

            { #SP2013 only

                Get-SPServer | ? {$_.Role -eq "Application"} | %{$Global:SPservers.add($_.Address)} | Out-Null

            }

            if([string]::IsNullOrEmpty($Global:SPservers ))

            {

                throw "Farm not found. Please run as a Farm Administrator and ensure this is run on a Sharepoint server that is part of a farm."

            }

            $buttonCreateNew.Enabled = $true

        }

        catch

        {

            if ($_.Exception.GetType().Name -eq "CommandNotFoundException")

            {

                $err = "No Sharepoint Farm Found. Please run this on a SharePoint server connected to a Farm"

            }

            else

            {

                $err=$S_

            }

            [Windows.Forms.MessageBox]::Show($err, "Error - FARM NOT FOUND", [Windows.Forms.MessageBoxButtons]::OK)

            Throw "Farm Failure"

        }

        #endregion Get Servers

 

        $arrSSA = New-Object System.Collections.ArrayList

        #$arrSSA = @()

 

        $ssa = Get-SPEnterpriseSearchServiceApplication #-Identity "Search Service Application"

        if ($ssa -eq $null)

        { # NO Search Service application Found

            $buttonImport.Enabled = $false

            throw " No SSA"

        }

        else

        {

            $ssa | Select Name, ID, Status | where-object {$_.Status -eq "Online"} | sort -Property Name | %{$arrSSA.add($_.Name)} | Out-Null

            $ddlSSA.DataSource = $arrSSA 

            $buttonImport.Enabled = $true

        }

    }

    catch

    {

        Display-Message "No Search Service Application found - Please create a Search Service Application" $errMessage

        throw "Exit"

    }

}

 

function Create-NewindexLocation

{

    $newIndexServers = New-Object System.Collections.ArrayList

    if ($Global:isIndexLocationchanged -or $Global:isNewSA -or $gridindex.items.count -le 0)

    {#Index Move, New SSA or No existing Index Components so add all servers into the array

        $ddIndex.CheckedItems | %{$newIndexServers.add($_)} | Out-Null

    }

    else

    {

        #Remove any Servers that exist so their folders are not deleted

        # Definitely need to re-look at the loops and optimise this

        # Select where-object failed to return values for some reason

       

        foreach ($IndexServer in $ddIndex.CheckedItems)

        {

            $indexExists = $false

            foreach($old in $Global:arrIndex)

            {

                If ($IndexServer -eq $old)

                {

                    $indexExists = $true

                }

            }

                if (!$indexExists)

                { #Doesn't exist so add it

                    $IndexServer | %{$newIndexServers.add($_)} | Out-Null

                }

 

         }

    }

 

    if ($newIndexServers.Count -gt 0)

    {# First check that there are Index Server changes

        #Check that a root folder location is not used

        if ($txtIndexNewLocation.Text.EndsWith('\') -or $txtIndexNewLocation.Text.EndsWith('/'))

        {

            $txtIndexNewLocation.Text = $txtIndexNewLocation.Text.Substring(0,$txtIndexNewLocation.Text.Length-1)

            if ($txtIndexNewLocation.Text.EndsWith(':'))

            {

                throw "Please specify a valid path. Root folder cannot be used."

            }

        }

        $path = "'" + $txtIndexNewLocation.Text + "'"

        $testpath = $path.Substring(0,3).Replace(':','$') + $path.Substring(3,$path.Length-3)

 

        try

        {#Check reserved locations as well - just in case

            if ($txtIndexNewLocation.Text.Contains(":\windows\") -or $txtIndexNewLocation.Text.Contains("\program files\") -or `

            $txtIndexNewLocation.Text.Contains("\program files (x86)\") -or $txtIndexNewLocation.Text.Contains(":\programdata\") -or`

            $txtIndexNewLocation.Text.Contains(":\inetpub\") -or $txtIndexNewLocation.Text.Contains(":\perflogs\") -or `

            $txtIndexNewLocation.Text.Contains(":\users\"))

            {

                throw "Please select a new location as the System or User locations cannot be used."

            }

            if ($txtIndexNewLocation.text.StartsWith($txtIndexLocation.Text + "\") -and ![string]::IsNullOrEmpty($txtIndexLocation.Text))

            {

                throw "New location can't be a subdirectory of old location as it gets deleted. Click IMPORT to reset and Select a new Folder Name or Location."

            }

 

            #First check that the location has no files in it.

            $scriptCheckEmpty = $ExecutionContext.InvokeCommand.NewScriptBlock("Get-ChildItem " + $path + " -force | Select-Object -First 1")

            #$ddIndex.Items | %{

            $newIndexServers | %{

            $emptycheck = Invoke-Command -ComputerName $_ -ScriptBlock $scriptCheckEmpty

            if (![string]::IsNullOrEmpty($emptycheck)){throw "Path $path is not Empty on all Index Servers. click IMPORT to reset and choose another location."}

            }

 

            #REMOVE AND RECREATE LOCATIONS ON ALL NEW INDEX SERVERS

            $scripttoremove      = $ExecutionContext.InvokeCommand.NewScriptBlock("Remove-Item -Recurse -Force -LiteralPath $path -ErrorAction SilentlyContinue")

            $scripttocreate      = $ExecutionContext.InvokeCommand.NewScriptBlock("mkdir $path")

 

            #$ddIndex.Items | %{

            $newIndexServers | %{

            Invoke-Command -ComputerName $_ -ScriptBlock $scripttoremove

            Invoke-Command -ComputerName $_ -ScriptBlock $scripttocreate 

            $scripttocheck  = $ExecutionContext.InvokeCommand.NewScriptBlock("Test-Path \\$_\" + $testpath.replace("'","") +" -PathType Container")

            $check = Invoke-Command -ComputerName $_ -ScriptBlock $scripttocheck 

            if (!$check) {throw "Path could not be created on $_. click IMPORT to reset and try again"}

 

            $scripttograntrights  = $ExecutionContext.InvokeCommand.NewScriptBlock("Icacls $path /grant '$_\WSS_WPG:(OI)(CI)F' /grant '$_\WSS_ADMIN_WPG:(OI)(CI)F'")

            #Grant full control to WSS_ADMIN_WPG, WSS_WPG - Check put in to cater for locked down folders

            Invoke-Command -ComputerName $_ -ScriptBlock $scripttograntrights 

 

            }

 

            Display-Message ("Index location "+$path+" created successfully on all Index Servers and folder permissions updated") $StatusMessage

            #$FormSearch.Refresh()

        }

        catch

        {

            Display-Message $_ $errMessage

            throw $_

        }

    }

}

 

function Remove-OldIndex

{

    try

    {

        # DELETE OLD INDEX LOCATION ON EACH SERVER ONLY IF NOT DEFAULT

        if ($txtIndexLocation.Text -ne "DEFAULT")

        {

            #DELETE OLD INDEX COMPONENT FROM INDEX LOCATION CHANGE

            #Put a sleep in to allow Components time to update

            Display-Message "Please wait - waiting for Topology changes to finish provisioning (5 Minutes)" $StatusMessage

            Start-Sleep -Seconds 300

 

            #Check that the Admin Components have had time to come online

            $searchApp = Get-SPEnterpriseSearchServiceApplication $ddlSSA.SelectedItem

            if (Check-AdminComponent $searchApp)

            { #If the Admin component has not come online then wait for it for 20 mins

                Initialize-AdminComponent $searchApp

            }

 

            # Refresh the variables with the new Topology

            $active = Get-SPEnterpriseSearchTopology -SearchApplication $ddlSSA.SelectedItem -Active

            $clone = New-SPEnterpriseSearchTopology -SearchApplication $ddlSSA.SelectedItem -Clone -SearchTopology $active

           

            Display-Message "INDEX LOCATION changed - Moving Index and Activating Topology" $StatusMessage

            #Set-SPEnterpriseSearchTopology -Identity $clone

            $IndextobeRemoved = New-Object system.collections.arraylist

            $Global:Components | Select Server, Name, Type | where-object {$_.Type -eq $Index} | sort -Property Server |%{$IndextobeRemoved.add($_)}  | Out-Null

 

            foreach($IndexComponent in $IndextobeRemoved)

            {

                Remove-SPEnterpriseSearchComponent -Identity $IndexComponent.Name -SearchTopology $clone -Confirm:$false

            }

            #Set-SPEnterpriseSearchTopology -Identity $clone

            $clone.activate()

            try

            {

                Remove-SPEnterpriseSearchTopology -Identity $active -Confirm:$false

            }

            catch

            { #Do nothing if the old topology will not allow deletion

 

            }

 

            Display-Message "Deleting old Index Location" $StatusMessage

            $scripttoremove = $ExecutionContext.InvokeCommand.NewScriptBlock("Remove-Item -Recurse -Force -LiteralPath " + $txtIndexLocation.Text + " -ErrorAction SilentlyContinue")

            $gridIndex.Items | %{

            Invoke-Command -ComputerName $_ -ScriptBlock $scripttoremove}

            Display-Message "New Topology Activated - old Topology Deleted" $StatusMessage

        }

         else

         {

            #ISSUE WITH DEFAULT ROOT DIRECTORY

            # C:\Program Files\Microsoft Office Servers\15.0\Data\Office Server\Applications\

            # If the default root directory is used when creating the Search Service Application then the value returned is NULL

            #$txtIndexLocation.Text = "C:\Program Files\Microsoft Office Servers\15.0\Data\Office Server\Applications\"

            # I couldn't find a way to find the SPnumber folder name so I leave it alone for now.

         }

    }

    catch

    {

        throw $_

    }

}

 

function Create-NewServiceApplication

{

    try

    {

        #Settings to Create a New Service Application

        $SearchAppPoolName           = $txtServiceAppName.Text+" App Pool"

        $SearchQryAppPoolName        = $txtServiceAppName.Text+" Query App Pool"

        $SearchAppPoolAccountName    = $txtServiceAcc.Text

        $SearchQryAppPoolAccountName = $txtQryServiceAcc.Text

        $SearchServiceName           = $txtServiceAppName.Text

        $SearchServiceProxyName      = "$SearchServiceName Proxy"

        $DatabaseServer = $txtDatabaseServer.Text

        $DatabaseName   = $txtDatabaseName.Text

        try

        {

            #Test Database Connectivity

            $connectTest = "Data Source=$DatabaseServer;Integrated Security=true;Initial Catalog=master;Connect Timeout=3;"

            $connection = new-object ("Data.SqlClient.SqlConnection") $connectTest

            if ($connection)

            {

                $connection.open()  

                if ($connection.State -eq 'Open')

                {

                    $connected =$true

                    $connection.Close();

                }

            }

            else

            {

                Throw "SQL error"

            }

        }

        catch

        {

            Throw "Unable to connect to SQL Server '"+$DatabaseServer+"'. Please check Server Name or firewall settings."

        }       

 

        #Create does not get run on an Existing service application so check the name

        # Faulty Service Applications can be fixed via the Import and modify

        $ServiceApplication = Get-SPEnterpriseSearchServiceApplication -Identity $SearchServiceName -ErrorAction SilentlyContinue

        if (!$ServiceApplication)

        { #Check that the databases do not exist in SharePoint

            $existingDatabase = Get-SPDatabase | ? { $_.Name -eq $DatabaseName }

            if($existingDatabase)

            {

                throw "The Search Database name $DatabaseName exists in the Farm - Please choose a different Name"

            }

       

            # VALIDATE Service Account used for admin app pool and Search Service Account

            $userAdmin = $txtServiceAcc.Text

            $passAdmin = (ConvertTo-SecureString  $txtServiceAccPass.Text -asPlaintext -force)

           

            $managedUserAdmin = Get-SPManagedAccount -Identity $userAdmin

            if (!$managedUserAdmin)

            {

                $CredentialAdmin  = New-Object –TypeName System.Management.Automation.PSCredential –ArgumentList $UserAdmin, $passAdmin

                $managedUserAdmin = New-SPManagedAccount –Credential $CredentialAdmin

                if (!$managedUserAdmin)

                {

                    throw "Invalid Search Admin Application Pool Account, please check Username and Password"

                }

            }

 

            $userQry = $txtQryServiceAcc.Text

            $passQry = (ConvertTo-SecureString  $txtQryServiceAccPass.Text -asPlaintext -force)

           

            $managedUserQry = Get-SPManagedAccount -Identity $userQry

            if (!$managedUserQry)

            { #Validate Service Account used by the Search Query App Pool

                $CredentialQry  = New-Object –TypeName System.Management.Automation.PSCredential –ArgumentList $userQry, $passQry

                $managedUserQry = New-SPManagedAccount –Credential $CredentialQry

                if (!$managedUserQry)

                {

                    throw "Invalid Search Query Application Pool Account, please check Username and Password"

                }

            }

           

 

            #Validate the Search Content account before proceeding

            $userCont = $txtContentAcc.Text

            $passCont = (ConvertTo-SecureString  $txtContentAccPass.Text -asPlaintext -force)

           

            $managedUserCont = Get-SPManagedAccount -Identity $userCont

            if (!$managedUsercont)

            { # ADD to Managed Account to test that it is valid and if we added it then we remove it afterwards.

                $CredentialCont  = New-Object –TypeName System.Management.Automation.PSCredential –ArgumentList $userCont, $passCont

                $managedUserCont = New-SPManagedAccount –Credential $CredentialCont

                if (!$managedUserCont)

                {

                    throw "Invalid Search Content Account, please check Username and Password"

                }

                #If we added it, then remove it as the Content Account is not a managed account

                Remove-SPManagedAccount -Identity $userCont -Confirm:$false

            }

 

            Display-Message "Creating Search Admin Service Application Pool" $StatusMessage

            #$FormSearch.Refresh()

 

            $SAAppPool = Get-SPServiceApplicationPool -Identity $SearchAppPoolName -ErrorAction SilentlyContinue

            

            if ($SAAppPool)

            {# If it exist and it's a new SSA then delete the App Pool first

                Remove-SPServiceApplicationPool $SAAppPool.Name -Confirm:$false

            }

            $SAAppPool = New-SPServiceApplicationPool -Name $SearchAppPoolName -Account $SearchAppPoolAccountName

            if (!$SAAppPool)

            {

               throw "Error creating Search Admin Service App Pool - Account validation failed"

            }

            Display-Message "Creating Search Query Service Application Pool" $StatusMessage

 

            $SAQryAppPool = Get-SPServiceApplicationPool -Identity $SearchQryAppPoolName -ErrorAction SilentlyContinue

            if ($SAQryAppPool)

            {

                Remove-SPServiceApplicationPool $SAQryAppPool.Name -Confirm:$false

            }

            #Create new SA Qry Pool

            $SAQryAppPool = New-SPServiceApplicationPool -Name $SearchQryAppPoolName -Account $SearchQryAppPoolAccountName

            if (!$SAQryAppPool)

            {

                throw "Error creating Search Query Service App Pool - Account validation failed"

            }

           

            Display-Message "Creating Search Service Application" $StatusMessage

            #$FormSearch.Refresh()

            

            New-SPEnterpriseSearchServiceApplication -Name $SearchServiceName -AdminApplicationPool $SAAppPool.Name -ApplicationPool $SAQryAppPool.Name -DatabaseServer  $DatabaseServer -DatabaseName $DatabaseName

            #Fetch it to make sure it has populated

            # Intermittet Issue where it is not available to the Proxy otherwise

            # Added a delay for timing as create throws an error occassionally as the Service App is not available yet.

            Start-Sleep -Seconds 5

            $ServiceApplication = Get-SPEnterpriseSearchServiceApplication -Identity $SearchServiceName

            if (!$ServiceApplication)

            {

                throw "Error creating Search Service Application - $SearchServiceName"

            }

 

            Display-Message "Creating Search Service Application Proxy" $StatusMessage

 

            $Proxy = Get-SPEnterpriseSearchServiceApplicationProxy -Identity $SearchServiceProxyName -ErrorAction SilentlyContinue

            if (!$Proxy)

            {

                $Proxy = New-SPEnterpriseSearchServiceApplicationProxy -Name $SearchServiceProxyName -SearchApplication $ServiceApplication

                if (!$Proxy)

                {

                    throw "Error Creating Search Service Application Proxy - $SearchServiceName"

                }

            }

 

            #Reload the Search Service Application and Select it

            load-SearchServiceApplication

            $ddlSSA.SelectedItem = $SearchServiceName

        }

        else

        {

            #Throw an error as the Service App already exists - The import topology should be used for this

            throw "The Service Application already exists - Please use the Import topology to Fix / Modify the Topology"

        }

    }

    catch

    {

        Display-Message $_ $errMessage

        Throw $_

    }

}

 

function Clear-CurrentTopology

{

    try

    {

        #Admin

        $gridAdmin.SelectedItem = $null

        Reset-ddItems $gridAdmin $true

 

        #Crawl

        $gridAdmin.SelectedItem = $null

        Reset-ddItems $gridCrawl $true

 

        #Content Processing

        $gridAdmin.SelectedItem = $null

        Reset-ddItems $gridContent $true

 

        #Index

        $gridAdmin.SelectedItem = $null

        Reset-ddItems $gridIndex $true

 

        #Query

        $gridAdmin.SelectedItem = $null

        Reset-ddItems $gridQuery $true

   

        #Analytics

        $gridAdmin.SelectedItem = $null

        Reset-ddItems $gridAnalytics $true

 

        $txtIndexLocation.Text = ""

 

    }

    catch

    {

        throw $_

    }

}

 

#region ALL Handlers

 

$buttonExit.add_click(

{$FormSearch.close() | Out-Null})

 

$buttonUpdate.add_click(

{

    Generate-Topology

})

 

$buttonImport.add_click(

{

    try

    {

        #Usage Service Application must be online

        checkforUsageSA

        if ($ddlSSA.Count -gt 0)

        {

            $buttonUpdate.Text   = "Modify Topology"

            $Global:isNewSA = $false

            $global:isIndexLocationChanged = $false

            Populate-CurrentConfig

        }

        else

        {

            Display-Message "No Search Service Application found - Create a Search Service Application first" $InfoMessage

        }

    }

    catch

    {

 

    }

})

 

 

$ddlSSA.add_SelectedIndexChanged(

{

    Display-Message "Select the Service Application and click IMPORT to Populate the current Topology." $InfoMessage

    $buttonUpdate.Enabled = $false

}

)

 

$txtIndexNewLocation.add_TextChanged(

{

    #Clean spaces before any checks

    $txtIndexLocation.Text=$txtIndexLocation.Text.Trim()

    $txtIndexNewLocation.Text=$txtIndexNewLocation.Text.Trim()

    if ($txtIndexLocation.Text.CompareTo($txtIndexNewLocation.Text) -and !$Global:isNewSA)

    {

        #Location has been changed and it's not a new Service App

        $global:isIndexLocationChanged = $true

        #Enable-NewTopology $false

    }

    else

    {

        $global:isIndexLocationChanged = $false

        if (![string]::IsNullOrWhiteSpace($txtIndexNewLocation.Text))

        {

            $buttonUpdate.enabled    = $true

        }

        #Enable-NewTopology $true

    }

}

)

 

#CHECK FOR USAGE AND HEALTH SERVICE APPLICATION

# Search Admin does not always come online if it doesn't exist

function checkforUsageSA

{

    try

    {

        #Usage and Health check

        $usageProxy = Get-SPServiceApplicationProxy | Where-Object{$_.TypeName -eq "Usage and Health Data Collection Proxy"}

        if($usageProxy.Status -ne "Online")

        {

            throw "Usage and Health Service Application Proxy is not Online - Start the Service Application and Provision the proxy"

        }

    }

    catch

    {

       Display-Message $_ $errMessage

       throw $_

       

    }

}

 

 

#OPEN CREATE NEW SERVICE APPLICATION FORM

$buttonCreateNew.add_click(

{

    try

    {

       checkforUsageSA

       $FormCreateNew.ShowDialog()

    }

    catch

    {

       throw $_

   }

})

 

$buttonCreateSA.add_click(

{#Create button clicked from Create New Form

    try

    {

        #Clean any whitespace of all captured fields

        $txtDatabaseName.Text      = $txtDatabaseName.Text.Trim()

        $txtDatabaseServer.Text    = $txtDatabaseServer.Text.Trim()

        $txtServiceAppName.Text    = $txtServiceAppName.Text.Trim()

        $txtServiceAcc.Text        = $txtServiceAcc.Text.Trim()

        $txtQryServiceAcc.Text     = $txtQryServiceAcc.Text.Trim()

        $txtQryServiceAccPass.Text = $txtQryServiceAccPass.Text.Trim()

        $txtServiceAccPass.Text    = $txtServiceAccPass.Text.Trim()

        $txtContentAcc.Text        = $txtContentAcc.Text.Trim()

        $txtContentAccPass.Text    = $txtContentAccPass.Text.Trim()

 

        if ($txtDatabaseName.Text -and $txtDatabaseServer.text -and $txtServiceAppName.text -and `

            $txtServiceAcc.text -and $txtQryServiceAcc.Text -and $txtQryServiceAccPass.Text -and `

            $txtServiceAccPass.Text -and $txtContentAcc.Text -and $txtContentAccPass.Text)

        {

           

            $FormCreateNew.Close()

           

            if(!$Global:isNewSA)

            {

                Populate-ServerDetails

                $txtIndexNewLocation.Text = ""

            }

            else

            {

                if ($txtIndexNewLocation.Text)

                {

                    $buttonUpdate.enabled = $true

                }

                else

                {

                    $buttonupdate.Enabled = $false

                }

            }

            Clear-CurrentTopology

            $txtIndexNewLocation.ReadOnly = $false

            $buttonUpdate.Text            = "Create Now"

 

            #if ($txtIndexNewLocation.Text)

            #{

            #    $buttonUpdate.enabled     = $true

            #}

            #else

            #{

            Display-Message "Select required Topology and enter an Index location e.g. d:\Index. Click CREATE NOW to start" $InfoMessage

            #}

            $ddlSSA.Enabled = $false

            $Global:isNewSA = $True

            #$formsearch.Refresh()

        }

    }

    catch

    {

        Display-Message $_ $errMessage

        throw $_

    }

})

 

#endregion ALL Handlers

 

# Execution FORM start

 

load-SearchServiceApplication

$FormSearch.ShowDialog()