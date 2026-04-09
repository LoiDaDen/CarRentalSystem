with open(r'd:\CarRentalSystem\CarRentalSystem\Areas\Admin\Views\User\Index.cshtml', 'r', encoding='utf-8') as f:
    text = f.read()

target_drop = """                                     @if (user.Role == "Admin")
                                     {
                                         <li>
                                             <form asp-action="SetRole" asp-route-id="@user.UserID" asp-route-role="Customer" method="post">
                                                 <button type="submit" class="dropdown-item rounded-3 fw-500">
                                                     <i class="bi bi-person-fill me-2 text-primary"></i>Hạ xuống Customer
                                                 </button>
                                             </form>
                                         </li>
                                     }
                                     <li><hr class="dropdown-divider my-1"></li>
                                     <li>
                                         <button class="dropdown-item rounded-3 text-danger fw-500" data-bs-toggle="modal" data-bs-target="#delUser-@user.UserID">
                                             <i class="bi bi-trash3 me-2"></i>Xóa tài khoản
                                         </button>
                                     </li>
                                 </ul>"""

replacement_drop = """                                     @if (user.Role == "Admin")
                                     {
                                         <li>
                                             <form asp-action="SetRole" asp-route-id="@user.UserID" asp-route-role="Customer" method="post">
                                                 <button type="submit" class="dropdown-item rounded-3 fw-500">
                                                     <i class="bi bi-person-fill me-2 text-primary"></i>Hạ xuống Customer
                                                 </button>
                                             </form>
                                         </li>
                                     }
                                     <li><hr class="dropdown-divider my-1"></li>
                                     <li>
                                         <button type="button" class="dropdown-item rounded-3 fw-500 text-info" data-bs-toggle="modal" data-bs-target="#ekycUser-@user.UserID">
                                             <i class="bi bi-person-vcard text-info me-2"></i>Duyệt giấy tờ (eKYC)
                                         </button>
                                     </li>
                                     <li>
                                         <form asp-action="ToggleBlacklist" asp-route-id="@user.UserID" method="post">
                                             <button type="submit" class="dropdown-item rounded-3 fw-500 text-dark">
                                                 <i class="bi bi-slash-circle me-2"></i>@(user.IsBlacklisted ? "Mở Khóa Tài Khoản" : "Đưa vào Danh sách Đen")
                                             </button>
                                         </form>
                                     </li>
                                     <li><hr class="dropdown-divider my-1"></li>
                                     <li>
                                         <button type="button" class="dropdown-item rounded-3 text-danger fw-500" data-bs-toggle="modal" data-bs-target="#delUser-@user.UserID">
                                             <i class="bi bi-trash3 me-2"></i>Xóa tài khoản
                                         </button>
                                     </li>
                                 </ul>"""

text = text.replace(target_drop, replacement_drop)

target_modal = """                                     <div class="d-flex gap-2">
                                         <button class="btn btn-light flex-grow-1 rounded-3" data-bs-dismiss="modal">Hủy</button>
                                         <form asp-action="Delete" asp-route-id="@user.UserID" method="post" class="flex-grow-1">
                                             <button type="submit" class="btn btn-danger w-100 rounded-3 fw-bold">Xóa</button>
                                         </form>
                                     </div>
                                 </div>
                             </div>
                         </div>
                     </div>"""

replacement_modal = """                                     <div class="d-flex gap-2">
                                         <button class="btn btn-light flex-grow-1 rounded-3" data-bs-dismiss="modal">Hủy</button>
                                         <form asp-action="Delete" asp-route-id="@user.UserID" method="post" class="flex-grow-1">
                                             <button type="submit" class="btn btn-danger w-100 rounded-3 fw-bold">Xóa</button>
                                         </form>
                                     </div>
                                 </div>
                             </div>
                         </div>
                     </div>

                     <!-- eKYC Modal -->
                     <div class="modal fade" id="ekycUser-@user.UserID" tabindex="-1">
                         <div class="modal-dialog modal-dialog-centered modal-lg">
                             <div class="modal-content border-0 shadow-lg rounded-4">
                                 <div class="modal-header border-0 pb-0">
                                     <h5 class="fw-bold mb-0">Hồ sơ eKYC: @user.Name</h5>
                                     <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                                 </div>
                                 <div class="modal-body p-4">
                                     <div class="row g-3 text-center">
                                         <div class="col-4">
                                             <div class="small fw-bold mb-2">CCCD (Mặt trước)</div>
                                             @if(!string.IsNullOrEmpty(user.IdCardFrontUrl)) { <img src="@user.IdCardFrontUrl" class="img-fluid rounded border" /> } else { <span class="text-muted small">Chưa có</span> }
                                         </div>
                                         <div class="col-4">
                                             <div class="small fw-bold mb-2">CCCD (Mặt sau)</div>
                                             @if(!string.IsNullOrEmpty(user.IdCardBackUrl)) { <img src="@user.IdCardBackUrl" class="img-fluid rounded border" /> } else { <span class="text-muted small">Chưa có</span> }
                                         </div>
                                         <div class="col-4">
                                             <div class="small fw-bold mb-2">GPLX</div>
                                             @if(!string.IsNullOrEmpty(user.DrivingLicenseUrl)) { <img src="@user.DrivingLicenseUrl" class="img-fluid rounded border" /> } else { <span class="text-muted small">Chưa có</span> }
                                         </div>
                                     </div>
                                     <div class="mt-4 pt-3 border-top text-end">
                                         <form asp-action="ToggleVerify" asp-route-id="@user.UserID" method="post" class="d-inline">
                                             <button type="submit" class="btn @(user.IsVerified ? "btn-outline-danger" : "btn-success") px-4 fw-bold rounded-pill">
                                                 @(user.IsVerified ? "Hủy Duyệt eKYC" : "Xác Nhận Hợp Lệ")
                                             </button>
                                         </form>
                                     </div>
                                 </div>
                             </div>
                         </div>
                     </div>"""
text = text.replace(target_modal, replacement_modal)

with open(r'd:\CarRentalSystem\CarRentalSystem\Areas\Admin\Views\User\Index.cshtml', 'w', encoding='utf-8') as f:
    f.write(text)
