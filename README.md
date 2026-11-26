# LangFix

โปรแกรม Windows 11 สำหรับแก้ข้อความที่พิมพ์ผิดภาษาระหว่างแป้นพิมพ์ไทย Kedmanee และอังกฤษ QWERTY

พัฒนาโดย Panuphong Bunkum

- ตั้งคีย์ลัดเองได้จากหน้าตั้งค่า (ค่าเริ่มต้น `Shift + Backspace`)
- กดคีย์ลัดซ้ำทันทีเพื่อย้อนกลับ หาก LangFix แก้คำไม่ตรงที่ต้องการ
- โหมดอัตโนมัติแก้คำที่มีความมั่นใจสูงทันทีเมื่อพิมพ์ครบ โดยไม่ต้องกด Space
- มีพจนานุกรมอังกฤษและไทยในตัว ไม่ต้องติดตั้ง RightLang
- ทำงานในเครื่อง ไม่ส่งหรือบันทึกประวัติการพิมพ์
- System Tray และเปิดพร้อม Windows
- ตรวจสอบการอัปเดตอัตโนมัติวันละครั้ง หรือกดตรวจเองจาก System Tray
- ส่งความคิดเห็น แจ้งปัญหา หรือขอฟังก์ชันเพิ่มจากเมนู System Tray

พจนานุกรมในตัวดัดแปลงจากพจนานุกรมโอเพนซอร์สของ LibreOffice โปรดดู `ThirdPartyNotices.txt`

การตรวจสอบอัปเดตเชื่อมต่อ [GitHub Releases](https://github.com/PanuphongBunkum/LangFix/releases/latest) เพื่ออ่านไฟล์ `langfix-version.json` ส่วนการแจ้งปัญหาและเสนอฟังก์ชันเปิดผ่าน [GitHub Issues](https://github.com/PanuphongBunkum/LangFix/issues)

ข้อจำกัด: แอปทั่วไปที่รันด้วยสิทธิ์ผู้ใช้ปกติไม่สามารถดักคีย์จากหน้าต่างที่รันเป็น Administrator ได้ และช่องรหัสผ่านบางชนิดอาจไม่เปิดเผยสถานะให้โปรแกรมภายนอกทราบ

## ดาวน์โหลด

ดาวน์โหลดตัวติดตั้งล่าสุดได้ที่ [GitHub Releases](https://github.com/PanuphongBunkum/LangFix/releases/latest)

## ออกเวอร์ชันใหม่ (สำหรับผู้พัฒนา)

```powershell
.\release.ps1 -Version 0.6.1 -Notes "สรุปสิ่งเปลี่ยนแปลง"
```
