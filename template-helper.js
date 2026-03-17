/**
 * Template Helper - Populates company document templates with data from SharePoint
 * 
 * This helper fetches company settings and other data from SharePoint and replaces
 * placeholders in HTML templates with actual values.
 * 
 * PLACEHOLDERS USED IN TEMPLATES:
 * 
 * Company Information:
 * - {{COMPANY_NAME}} - Company name from Company Settings
 * - {{COMPANY_NUMBER}} - Company registration number from Company Settings
 * - {{REGISTERED_OFFICE_ADDRESS}} - Registered address from Company Settings
 * 
 * Shareholder/Certificate Information:
 * - {{HOLDER_FULL_NAME}} - Shareholder full name
 * - {{SHARE_CLASS}} - Share class name (A Ordinary, B Ordinary, etc.)
 * - {{NUMBER_OF_SHARES}} - Number of shares owned
 * - {{ISSUE_DATE_DD_MON_YYYY}} - Date formatted as "01 Jan 2026"
 * - {{FROM_TO_NUMBERS}} - Share number range (e.g., "1-100")
 * - {{YEAR}} - Current year or certificate year
 * - {{SEQUENCE}} - Sequential number for certificates/vouchers
 * 
 * Dividend Information:
 * - {{MEETING_DATE}} - Date of board meeting
 * - {{MEETING_TIME_AND_PLACE}} - Time and place of meeting
 * - {{DIRECTOR_NAME}} - Director's name
 * - {{ACCOUNTS_TO_DATE}} - Accounts period end date
 * - {{AMOUNT_PER_SHARE}} - Dividend amount per share
 * - {{PAYMENT_DATE}} - When dividend will be paid
 * - {{RECORD_DATE}} - Record date for dividend eligibility
 * - {{SIGN_DATE}} - Date of signature
 * - {{GROSS_AMOUNT}} - Gross dividend amount
 * - {{NET_AMOUNT}} - Net dividend amount
 * - {{AUTHORIZED_OFFICER_NAME}} - Name of authorized officer (if different from director)
 * 
 * USAGE:
 * 
 * 1. From JavaScript:
 *    const helper = new TemplateHelper();
 *    await helper.initialize();
 *    const html = await helper.populateTemplate('Share Certificate — A Ordinary Shares.html', data);
 * 
 * 2. From React Component:
 *    import { populateDocumentTemplate } from './template-helper.js';
 *    const filledTemplate = await populateDocumentTemplate(templateName, shareholderData);
 */

class TemplateHelper {
    constructor() {
        this.companySettings = null;
        this.apiBase = window.API_BASE || '/api';
    }

    /**
     * Initialize by fetching company settings from SharePoint
     */
    async initialize() {
        try {
            const response = await fetch(`${this.apiBase}/companysettings`);
            if (!response.ok) throw new Error('Failed to fetch company settings');
            this.companySettings = await response.json();
        } catch (error) {
            console.error('Error initializing TemplateHelper:', error);
            throw error;
        }
    }

    /**
     * Load a template file
     * @param {string} templateName - Name of the template file
     * @returns {Promise<string>} Template HTML content
     */
    async loadTemplate(templateName) {
        try {
            const response = await fetch(`/${templateName}`);
            if (!response.ok) throw new Error(`Failed to load template: ${templateName}`);
            return await response.text();
        } catch (error) {
            console.error('Error loading template:', error);
            throw error;
        }
    }

    /**
     * Format a date as "01 Jan 2026"
     * @param {Date|string} date - Date to format
     * @returns {string} Formatted date
     */
    formatDate(date) {
        if (!date) return '';
        const d = typeof date === 'string' ? new Date(date) : date;
        const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        return `${String(d.getDate()).padStart(2, '0')} ${months[d.getMonth()]} ${d.getFullYear()}`;
    }

    /**
     * Format a date as "DD/MM/YYYY"
     * @param {Date|string} date - Date to format
     * @returns {string} Formatted date
     */
    formatDateDMY(date) {
        if (!date) return '';
        const d = typeof date === 'string' ? new Date(date) : date;
        return `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}`;
    }

    /**
     * Replace placeholders in template with actual values
     * @param {string} template - Template HTML
     * @param {object} data - Data to fill in
     * @returns {string} Populated template
     */
    populatePlaceholders(template, data) {
        let result = template;

        // Company information (from Company Settings)
        if (this.companySettings) {
            result = result.replace(/\{\{COMPANY_NAME\}\}/g, this.companySettings.companyName || this.companySettings.businessName || '');
            result = result.replace(/\{\{COMPANY_NUMBER\}\}/g, this.companySettings.companyRegistrationNumber || this.companySettings.companyRegNo || '');
            result = result.replace(/\{\{REGISTERED_OFFICE_ADDRESS\}\}/g, this.companySettings.companyAddress || this.companySettings.registeredAddress || this.companySettings.address || '');
        }

        // All other placeholders from provided data
        Object.keys(data).forEach(key => {
            const placeholder = `{{${key.toUpperCase()}}}`;
            const regex = new RegExp(placeholder.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'g');
            result = result.replace(regex, data[key] || '');
        });

        return result;
    }

    /**
     * Generate a share certificate
     * @param {string} templateName - Template file name (e.g., 'Share Certificate — A Ordinary Shares.html')
     * @param {object} shareholderData - Shareholder and certificate data
     * @returns {Promise<string>} Populated certificate HTML
     */
    async generateShareCertificate(templateName, shareholderData) {
        if (!this.companySettings) {
            await this.initialize();
        }

        const template = await this.loadTemplate(templateName);
        
        const data = {
            HOLDER_FULL_NAME: shareholderData.shareholderName,
            NUMBER_OF_SHARES: shareholderData.sharesOwned,
            ISSUE_DATE_DD_MON_YYYY: this.formatDate(shareholderData.issueDate || new Date()),
            FROM_TO_NUMBERS: shareholderData.shareNumberRange || `1-${shareholderData.sharesOwned}`,
            YEAR: new Date().getFullYear(),
            SEQUENCE: shareholderData.certificateNumber || '001',
            DIRECTOR_NAME: shareholderData.directorName || '',
            SIGN_DATE: this.formatDateDMY(new Date()),
            ...shareholderData
        };

        return this.populatePlaceholders(template, data);
    }

    /**
     * Generate board minutes for dividend declaration
     * @param {object} dividendData - Dividend meeting data
     * @returns {Promise<string>} Populated minutes HTML
     */
    async generateBoardMinutes(dividendData) {
        if (!this.companySettings) {
            await this.initialize();
        }

        const template = await this.loadTemplate('Board Minutes — Declaring a Dividend.html');
        
        const data = {
            MEETING_DATE: this.formatDate(dividendData.meetingDate || new Date()),
            MEETING_TIME_AND_PLACE: dividendData.meetingTimePlace || 'Via video conference',
            DIRECTOR_NAME: dividendData.directorName || '',
            ACCOUNTS_TO_DATE: this.formatDate(dividendData.accountsDate),
            SHARE_CLASS: dividendData.shareClass || 'Ordinary',
            AMOUNT_PER_SHARE: dividendData.amountPerShare || '0.00',
            PAYMENT_DATE: this.formatDate(dividendData.paymentDate),
            RECORD_DATE: this.formatDate(dividendData.recordDate),
            SIGN_DATE: this.formatDateDMY(new Date()),
            AUTHORIZED_OFFICER_NAME: dividendData.authorizedOfficerName || dividendData.directorName || '',
            ...dividendData
        };

        return this.populatePlaceholders(template, data);
    }

    /**
     * Generate dividend voucher for a shareholder
     * @param {object} voucherData - Voucher data
     * @returns {Promise<string>} Populated voucher HTML
     */
    async generateDividendVoucher(voucherData) {
        if (!this.companySettings) {
            await this.initialize();
        }

        const template = await this.loadTemplate('Dividend Voucher — per Shareholder.html');
        
        const data = {
            YEAR: new Date().getFullYear(),
            SEQUENCE: voucherData.voucherNumber || '001',
            HOLDER_FULL_NAME: voucherData.shareholderName,
            SHARE_CLASS: voucherData.shareClass,
            NUMBER_OF_SHARES: voucherData.numberOfShares,
            AMOUNT_PER_SHARE: voucherData.amountPerShare || '0.00',
            PAYMENT_DATE: this.formatDate(voucherData.paymentDate),
            RECORD_DATE: this.formatDate(voucherData.recordDate),
            GROSS_AMOUNT: voucherData.grossAmount || '0.00',
            NET_AMOUNT: voucherData.netAmount || voucherData.grossAmount || '0.00',
            DIRECTOR_NAME: voucherData.directorName || '',
            SIGN_DATE: this.formatDateDMY(new Date()),
            ...voucherData
        };

        return this.populatePlaceholders(template, data);
    }
}

// Export for use in other modules
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { TemplateHelper };
}

// Export functions for easier use
async function populateDocumentTemplate(templateName, data) {
    const helper = new TemplateHelper();
    await helper.initialize();
    const template = await helper.loadTemplate(templateName);
    return helper.populatePlaceholders(template, data);
}

if (typeof module !== 'undefined' && module.exports) {
    module.exports.populateDocumentTemplate = populateDocumentTemplate;
}
